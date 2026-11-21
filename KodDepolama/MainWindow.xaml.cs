using DevSnippetManager.Services;
using DevSnippetManager.ViewModels;
using DevSnippetManager.Views;
using Microsoft.Web.WebView2.Core;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Interop;
using Drawing = System.Drawing;
using WinForms = System.Windows.Forms;

namespace KodDepolama
{
    public partial class MainWindow : Window
    {
        [DllImport("user32.dll")]
        private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

        [DllImport("user32.dll")]
        private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

        private const int HOTKEY_ID = 9000;
        private const int HOTKEY_QUICK_SEARCH_ID = 9001;
        private const uint MOD_CONTROL = 0x0002;
        private const uint MOD_SHIFT = 0x0004;
        private const uint VK_S = 0x53; // 'S' key
        private const uint VK_SPACE = 0x20; // Space key

        private MainViewModel _viewModel;
        private bool _editorReady;
        private string _pendingEditorCode = string.Empty;
        private string _pendingEditorLanguage = "csharp";

        private WinForms.NotifyIcon? _notifyIcon;
        private Drawing.Icon? _extractedTrayIcon;
        private QuickSearchWindow? _quickSearchWindow;
        private bool _isExiting;
        private bool _trayBalloonShown;

        public MainWindow()
        {
            InitializeComponent();
            _viewModel = (MainViewModel)DataContext;

            // Setup WebView2 and Monaco bridge
            _viewModel.UpdateEditorCodeAction = (code) =>
            {
                _pendingEditorCode = code ?? string.Empty;
                if (_editorReady && webView.CoreWebView2 != null)
                {
                    ApplyEditorCode();
                }
            };

            _viewModel.UpdateEditorLanguageAction = (language) =>
            {
                _pendingEditorLanguage = LanguageCatalog.GetMonacoId(language);
                if (_editorReady && webView.CoreWebView2 != null)
                {
                    ApplyEditorLanguage();
                }
            };

            _viewModel.GetEditorCodeFunc = async () =>
            {
                if (webView.CoreWebView2 != null)
                {
                    var json = await webView.CoreWebView2.ExecuteScriptAsync("window.editor ? window.editor.getValue() : null;");
                    return JsonSerializer.Deserialize<string>(json) ?? string.Empty;
                }
                return string.Empty;
            };

            _viewModel.RequestTemplateValuesFunc = RequestTemplateValuesAsync;

            InitializeTrayIcon();
            InitializeAsync();
        }

        private void InitializeTrayIcon()
        {
            Drawing.Icon icon = Drawing.SystemIcons.Application;
            try
            {
                if (!string.IsNullOrWhiteSpace(Environment.ProcessPath))
                {
                    _extractedTrayIcon = Drawing.Icon.ExtractAssociatedIcon(Environment.ProcessPath);
                    if (_extractedTrayIcon != null)
                    {
                        icon = _extractedTrayIcon;
                    }
                }
            }
            catch
            {
                // Fall back to default application icon
            }

            var menu = new WinForms.ContextMenuStrip();
            menu.Items.Add("Aç", null, (_, _) => Dispatcher.Invoke(ShowAndActivateMainWindow));
            menu.Items.Add("Yeni snippet", null, (_, _) => Dispatcher.Invoke(() =>
            {
                ShowAndActivateMainWindow();
                _viewModel.NewSnippetCommand.Execute(null);
            }));
            menu.Items.Add("Panodan yeni snippet", null, (_, _) => Dispatcher.Invoke(() =>
            {
                ShowAndActivateMainWindow();
                _viewModel.NewFromClipboardCommand.Execute(null);
            }));
            menu.Items.Add("Hızlı ara (Ctrl+Shift+Space)", null, (_, _) => Dispatcher.Invoke(ToggleQuickSearch));
            menu.Items.Add(new WinForms.ToolStripSeparator());

            var minimizeItem = new WinForms.ToolStripMenuItem("Kapatınca tepsiye küçült")
            {
                CheckOnClick = true,
                Checked = _viewModel.Settings.MinimizeToTray
            };
            minimizeItem.CheckedChanged += (_, _) =>
            {
                _viewModel.Settings.MinimizeToTray = minimizeItem.Checked;
                _viewModel.SaveSettings();
            };
            menu.Items.Add(minimizeItem);
            menu.Items.Add(new WinForms.ToolStripSeparator());
            menu.Items.Add("Çıkış", null, (_, _) => Dispatcher.Invoke(ExitApplication));

            _notifyIcon = new WinForms.NotifyIcon
            {
                Icon = icon,
                Text = "DevVault",
                ContextMenuStrip = menu,
                Visible = true
            };
            _notifyIcon.DoubleClick += (_, _) => Dispatcher.Invoke(ShowAndActivateMainWindow);
        }

        private void ShowAndActivateMainWindow()
        {
            if (!IsVisible)
            {
                Show();
            }

            if (WindowState == WindowState.Minimized)
            {
                WindowState = WindowState.Normal;
            }

            Activate();
            Topmost = true;
            Topmost = false;
            Focus();
        }

        private void ToggleQuickSearch()
        {
            _quickSearchWindow ??= new QuickSearchWindow(_viewModel, snippet =>
            {
                ShowAndActivateMainWindow();
                _viewModel.TrySelectSnippet(snippet);
            });

            if (_quickSearchWindow.IsVisible)
            {
                _quickSearchWindow.Hide();
            }
            else
            {
                _quickSearchWindow.ShowAndFocus();
            }
        }

        private Task<IReadOnlyDictionary<string, string>?> RequestTemplateValuesAsync(IReadOnlyList<TemplateVariable> variables)
        {
            return Dispatcher.Invoke(() =>
            {
                var dialog = new TemplateVariablesWindow(variables);
                var quickSearchVisible = _quickSearchWindow != null && _quickSearchWindow.IsVisible;

                if (quickSearchVisible)
                {
                    _quickSearchWindow!.SuppressDeactivateHide = true;
                    dialog.Owner = _quickSearchWindow;
                    dialog.Topmost = _quickSearchWindow.Topmost;
                    dialog.WindowStartupLocation = WindowStartupLocation.CenterOwner;
                }
                else if (IsVisible)
                {
                    dialog.Owner = this;
                    dialog.WindowStartupLocation = WindowStartupLocation.CenterOwner;
                }
                else
                {
                    dialog.WindowStartupLocation = WindowStartupLocation.CenterScreen;
                    dialog.Topmost = true;
                }

                try
                {
                    var accepted = dialog.ShowDialog() == true;
                    return Task.FromResult<IReadOnlyDictionary<string, string>?>(accepted ? dialog.ResultValues : null);
                }
                finally
                {
                    if (quickSearchVisible && _quickSearchWindow != null)
                    {
                        _quickSearchWindow.SuppressDeactivateHide = false;
                    }
                }
            });
        }

        public void PrepareForExit()
        {
            _isExiting = true;
        }

        private void ExitApplication()
        {
            if (!_viewModel.ConfirmDiscardChanges())
            {
                return;
            }

            _isExiting = true;
            Close();
            System.Windows.Application.Current.Shutdown();
        }

        private async void InitializeAsync()
        {
            await webView.EnsureCoreWebView2Async();
            webView.CoreWebView2.SetVirtualHostNameToFolderMapping(
                "devvault.local",
                AppDomain.CurrentDomain.BaseDirectory,
                CoreWebView2HostResourceAccessKind.Allow);
            webView.CoreWebView2.WebMessageReceived += CoreWebView2_WebMessageReceived;
            webView.Source = new Uri("https://devvault.local/MonacoEditor.html");
        }

        private void CoreWebView2_WebMessageReceived(object? sender, CoreWebView2WebMessageReceivedEventArgs e)
        {
            using var message = JsonDocument.Parse(e.WebMessageAsJson);
            var root = message.RootElement;
            var type = root.TryGetProperty("type", out var typeElement) ? typeElement.GetString() : null;

            Dispatcher.Invoke(() =>
            {
                if (type == "ready")
                {
                    _editorReady = true;
                    ApplyEditorCode();
                    ApplyEditorLanguage();
                    return;
                }

                if (type == "changed")
                {
                    _viewModel.MarkCodeChanged();
                    return;
                }

                if (type == "command" && root.TryGetProperty("name", out var nameElement))
                {
                    switch (nameElement.GetString())
                    {
                        case "save":
                            _viewModel.SaveSnippetCommand.Execute(null);
                            break;
                        case "new":
                            _viewModel.NewSnippetCommand.Execute(null);
                            break;
                        case "search":
                            SearchBox.Focus();
                            break;
                        case "copy":
                            _viewModel.CopySnippetCommand.Execute(null);
                            break;
                    }
                }
            });
        }

        private void ApplyEditorCode()
        {
            if (!_editorReady || webView.CoreWebView2 == null) return;
            var encoded = JsonSerializer.Serialize(_pendingEditorCode);
            webView.CoreWebView2.ExecuteScriptAsync($"window.setEditorCode({encoded});");
        }

        private void ApplyEditorLanguage()
        {
            if (!_editorReady || webView.CoreWebView2 == null) return;
            var encoded = JsonSerializer.Serialize(_pendingEditorLanguage);
            webView.CoreWebView2.ExecuteScriptAsync($"window.setLanguage({encoded});");
        }

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);
            IntPtr handle = new WindowInteropHelper(this).Handle;
            HwndSource source = HwndSource.FromHwnd(handle);
            source.AddHook(HwndHook);

            var failedHotkeys = new List<string>();
            if (!RegisterHotKey(handle, HOTKEY_ID, MOD_CONTROL | MOD_SHIFT, VK_S))
            {
                failedHotkeys.Add("Ctrl+Shift+S");
            }
            if (!RegisterHotKey(handle, HOTKEY_QUICK_SEARCH_ID, MOD_CONTROL | MOD_SHIFT, VK_SPACE))
            {
                failedHotkeys.Add("Ctrl+Shift+Space");
            }

            if (failedHotkeys.Count > 0)
            {
                var message = $"Kısayol kaydedilemedi: {string.Join(", ", failedHotkeys)} (başka bir uygulama kullanıyor olabilir)";
                _viewModel.MessageQueue.Enqueue(message);
                _notifyIcon?.ShowBalloonTip(4000, "DevVault", message, WinForms.ToolTipIcon.Warning);
            }
        }

        private IntPtr HwndHook(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (App.ShowExistingInstanceMessage != 0 && (uint)msg == App.ShowExistingInstanceMessage)
            {
                ShowAndActivateMainWindow();
                handled = true;
                return IntPtr.Zero;
            }

            const int WM_HOTKEY = 0x0312;
            if (msg == WM_HOTKEY)
            {
                var id = wParam.ToInt32();
                if (id == HOTKEY_ID)
                {
                    ShowAndActivateMainWindow();
                    _viewModel.NewSnippetCommand.Execute(null);
                    handled = true;
                }
                else if (id == HOTKEY_QUICK_SEARCH_ID)
                {
                    ToggleQuickSearch();
                    handled = true;
                }
            }
            return IntPtr.Zero;
        }

        protected override void OnClosing(CancelEventArgs e)
        {
            var exiting = _isExiting || App.IsSystemShuttingDown;

            if (!exiting && _viewModel.Settings.MinimizeToTray)
            {
                e.Cancel = true;
                Hide();

                if (!_trayBalloonShown)
                {
                    _trayBalloonShown = true;
                    _notifyIcon?.ShowBalloonTip(
                        3000,
                        "DevVault",
                        "DevVault arka planda çalışmaya devam ediyor. Çıkmak için tepsi simgesine sağ tıklayın.",
                        WinForms.ToolTipIcon.Info);
                }
                return;
            }

            if (!exiting && !_viewModel.ConfirmDiscardChanges())
            {
                e.Cancel = true;
                return;
            }

            _isExiting = true;
            base.OnClosing(e);
        }

        protected override void OnClosed(EventArgs e)
        {
            IntPtr handle = new WindowInteropHelper(this).Handle;
            UnregisterHotKey(handle, HOTKEY_ID);
            UnregisterHotKey(handle, HOTKEY_QUICK_SEARCH_ID);

            if (_quickSearchWindow != null)
            {
                _quickSearchWindow.AllowClose = true;
                _quickSearchWindow.Close();
                _quickSearchWindow = null;
            }

            if (_notifyIcon != null)
            {
                _notifyIcon.Visible = false;
                _notifyIcon.Dispose();
                _notifyIcon = null;
            }

            _extractedTrayIcon?.Dispose();
            _extractedTrayIcon = null;

            base.OnClosed(e);
        }

        protected override void OnKeyDown(System.Windows.Input.KeyEventArgs e)
        {
            base.OnKeyDown(e);
            if (e.Key == Key.S && Keyboard.Modifiers == ModifierKeys.Control)
            {
                _viewModel.SaveSnippetCommand.Execute(null);
                e.Handled = true;
            }
            else if (e.Key == Key.N && Keyboard.Modifiers == ModifierKeys.Control)
            {
                _viewModel.NewSnippetCommand.Execute(null);
                e.Handled = true;
            }
            else if (e.Key == Key.F && Keyboard.Modifiers == ModifierKeys.Control)
            {
                SearchBox.Focus();
                e.Handled = true;
            }
            else if (e.Key == Key.C && Keyboard.Modifiers == (ModifierKeys.Control | ModifierKeys.Shift))
            {
                _viewModel.CopySnippetCommand.Execute(null);
                e.Handled = true;
            }
            else if (e.Key == Key.V && Keyboard.Modifiers == (ModifierKeys.Control | ModifierKeys.Shift))
            {
                if (Keyboard.FocusedElement is not System.Windows.Controls.Primitives.TextBoxBase && !webView.IsKeyboardFocusWithin)
                {
                    _viewModel.NewFromClipboardCommand.Execute(null);
                    e.Handled = true;
                }
            }
        }

        private void BtnShortcuts_Click(object sender, RoutedEventArgs e)
        {
            var msg = "Global Kısayollar:\n" +
                      "- Ctrl + Shift + Space : Hızlı arama (her yerden)\n" +
                      "- Ctrl + Shift + S : Yeni kod penceresini aç (uygulama tepside çalışırken)\n\n" +
                      "Uygulama İçi Kısayollar:\n" +
                      "- Ctrl + S : Kaydet\n" +
                      "- Ctrl + N : Yeni\n" +
                      "- Ctrl + Shift + V : Panodan yeni snippet\n" +
                      "- Ctrl + F : Arama\n" +
                      "- Ctrl + Shift + C : Kopyala";
            System.Windows.MessageBox.Show(msg, "Kısayollar", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }
}
