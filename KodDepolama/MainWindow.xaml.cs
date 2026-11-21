using DevSnippetManager.Services;
using DevSnippetManager.ViewModels;
using DevSnippetManager.Views;
using MdXaml;
using Microsoft.Web.WebView2.Core;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Media3D;
using System.Windows.Threading;
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
        private readonly DispatcherTimer _editorLoadTimer = new()
        {
            Interval = TimeSpan.FromSeconds(15)
        };
        private string _pendingEditorCode = string.Empty;
        private string _pendingEditorLanguage = "csharp";
        private MainViewModel.EditorOptions _pendingEditorOptions = new("vs-dark", 14, false, false);

        private WinForms.NotifyIcon? _notifyIcon;
        private WinForms.ToolStripMenuItem? _minimizeToTrayMenuItem;
        private bool _updatingTrayMenu;
        private Drawing.Icon? _extractedTrayIcon;
        private QuickSearchWindow? _quickSearchWindow;
        private bool _isExiting;
        private bool _trayBalloonShown;

        public MainWindow()
        {
            InitializeComponent();
            _viewModel = (MainViewModel)DataContext;
            _pendingEditorOptions = _viewModel.CurrentEditorOptions;
            _editorLoadTimer.Tick += EditorLoadTimer_Tick;
            webView.DefaultBackgroundColor = _viewModel.IsLightTheme
                ? Drawing.Color.FromArgb(255, 255, 255, 254)
                : Drawing.Color.FromArgb(255, 30, 30, 30);

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

            _viewModel.UpdateEditorOptionsAction = (options) =>
            {
                _pendingEditorOptions = options;
                webView.DefaultBackgroundColor = options.MonacoTheme == "vs"
                    ? Drawing.Color.FromArgb(255, 255, 255, 254)
                    : Drawing.Color.FromArgb(255, 30, 30, 30);
                if (_editorReady && webView.CoreWebView2 != null)
                    ApplyEditorOptions();
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
                if (_updatingTrayMenu) return;
                _viewModel.Settings.MinimizeToTray = minimizeItem.Checked;
                _viewModel.SaveSettings();
            };
            _minimizeToTrayMenuItem = minimizeItem;
            menu.Items.Add(minimizeItem);
            menu.Items.Add("Ayarlar…", null, (_, _) => Dispatcher.Invoke(OpenSettingsWindow));
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
            _viewModel.FlushPendingSettings();
            Close();
            System.Windows.Application.Current.Shutdown();
        }

        private async void InitializeAsync()
        {
            try
            {
                await webView.EnsureCoreWebView2Async();
                webView.CoreWebView2.Settings.IsZoomControlEnabled = false;
                webView.ZoomFactor = 1.0;
                var options = new
                {
                    theme = _pendingEditorOptions.MonacoTheme,
                    fontSize = _pendingEditorOptions.FontSize,
                    wordWrap = _pendingEditorOptions.WordWrap,
                    minimap = _pendingEditorOptions.Minimap
                };
                var encodedOptions = JsonSerializer.Serialize(options);
                await webView.CoreWebView2.AddScriptToExecuteOnDocumentCreatedAsync($"window.__initOpts = {encodedOptions};");
                webView.CoreWebView2.SetVirtualHostNameToFolderMapping(
                    "devvault.local",
                    AppDomain.CurrentDomain.BaseDirectory,
                    CoreWebView2HostResourceAccessKind.Allow);
                webView.CoreWebView2.WebMessageReceived += CoreWebView2_WebMessageReceived;
                webView.CoreWebView2.NavigationCompleted += CoreWebView2_NavigationCompleted;
                webView.Source = new Uri("https://devvault.local/MonacoEditor.html");
                _editorLoadTimer.Stop();
                _editorLoadTimer.Start();
            }
            catch (Exception ex)
            {
                webView.Visibility = Visibility.Visible;
                EditorErrorText.Text = $"Editör yüklenemedi: {ex.Message}";
                EditorErrorText.Visibility = Visibility.Visible;
            }
        }

        private void CoreWebView2_NavigationCompleted(object? sender, CoreWebView2NavigationCompletedEventArgs e)
        {
            if (e.IsSuccess)
                return;

            _editorLoadTimer.Stop();
            webView.Visibility = Visibility.Visible;
            EditorErrorText.Text = $"Editör yüklenemedi (gezinme hatası: {e.WebErrorStatus}).";
            EditorErrorText.Visibility = Visibility.Visible;
        }

        private void EditorLoadTimer_Tick(object? sender, EventArgs e)
        {
            _editorLoadTimer.Stop();
            if (_editorReady)
                return;

            webView.Visibility = Visibility.Visible;
            EditorErrorText.Text = "Editör yüklenmesi beklenenden uzun sürüyor…";
            EditorErrorText.Visibility = Visibility.Visible;
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
                    _editorLoadTimer.Stop();
                    _editorReady = true;
                    webView.Visibility = Visibility.Visible;
                    EditorErrorText.Visibility = Visibility.Collapsed;
                    ApplyEditorCode();
                    ApplyEditorLanguage();
                    ApplyEditorOptions();
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
                        case "fontInc":
                            _viewModel.IncreaseFontSizeCommand.Execute(null);
                            break;
                        case "fontDec":
                            _viewModel.DecreaseFontSizeCommand.Execute(null);
                            break;
                        case "fontReset":
                            _viewModel.ResetFontSizeCommand.Execute(null);
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

        private void ApplyEditorOptions()
        {
            if (!_editorReady || webView.CoreWebView2 == null) return;
            var options = new
            {
                theme = _pendingEditorOptions.MonacoTheme,
                fontSize = _pendingEditorOptions.FontSize,
                wordWrap = _pendingEditorOptions.WordWrap,
                minimap = _pendingEditorOptions.Minimap
            };
            var encoded = JsonSerializer.Serialize(options);
            webView.CoreWebView2.ExecuteScriptAsync($"window.setEditorOptions({encoded});");
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
            _editorLoadTimer.Stop();
            _viewModel.FlushPendingSettings();
            base.OnClosing(e);
        }

        protected override void OnClosed(EventArgs e)
        {
            _editorLoadTimer.Stop();
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

        public void RefreshTrayMinimizeMenuItem()
        {
            if (_minimizeToTrayMenuItem == null) return;
            _updatingTrayMenu = true;
            try
            {
                _minimizeToTrayMenuItem.Checked = _viewModel.Settings.MinimizeToTray;
            }
            finally
            {
                _updatingTrayMenu = false;
            }
        }

        private void OpenSettingsWindow()
        {
            ShowAndActivateMainWindow();
            var settingsWindow = new SettingsWindow(_viewModel)
            {
                Owner = this
            };
            settingsWindow.ShowDialog();
            RefreshTrayMinimizeMenuItem();
        }

        private void BtnSettings_Click(object sender, RoutedEventArgs e)
        {
            OpenSettingsWindow();
        }

        private void SidebarSplitter_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            _viewModel.SidebarWidth = 340;
        }

        private void Window_Loaded(object sender, RoutedEventArgs e) => UpdateEffectiveSidebarMaxWidth();

        private void Window_SizeChanged(object sender, SizeChangedEventArgs e) => UpdateEffectiveSidebarMaxWidth();

        private void UpdateEffectiveSidebarMaxWidth()
        {
            var availableWidth = MainContentGrid.ActualWidth - 560 - 8;
            SidebarColumn.MaxWidth = Math.Max(260, Math.Min(600, availableWidth));
        }

        private static DependencyObject? GetParentElement(object? element)
        {
            if (element is Visual || element is Visual3D)
                return VisualTreeHelper.GetParent((DependencyObject)element);
            if (element is ContentElement contentElement)
                return ContentOperations.GetParent(contentElement);
            if (element is FrameworkElement frameworkElement)
                return frameworkElement.Parent;
            if (element is FrameworkContentElement frameworkContentElement)
                return frameworkContentElement.Parent;
            return null;
        }

        private static bool IsInside<T>(object? source) where T : class
        {
            return FindAncestor<T>(source) != null;
        }

        private static T? FindAncestor<T>(object? source) where T : class
        {
            object? current = source;
            while (current != null)
            {
                if (current is T match)
                    return match;
                current = GetParentElement(current);
            }
            return null;
        }

        private static bool HasTextSelection(object? source)
        {
            object? current = source;
            while (current != null)
            {
                if (current is System.Windows.Controls.TextBox textBox && textBox.SelectionLength > 0)
                    return true;
                if (current is System.Windows.Controls.RichTextBox richTextBox && !richTextBox.Selection.IsEmpty)
                    return true;
                if (current is MarkdownScrollViewer markdownViewer)
                {
                    var selectionLength = markdownViewer.GetType().GetProperty("SelectionLength")?.GetValue(markdownViewer);
                    if (selectionLength != null && Convert.ToInt32(selectionLength) > 0)
                        return true;
                }
                current = GetParentElement(current);
            }
            return false;
        }

        private void DescriptionPreview_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (IsInside<System.Windows.Controls.Primitives.ScrollBar>(e.OriginalSource) ||
                IsInside<Thumb>(e.OriginalSource) ||
                IsInside<RepeatButton>(e.OriginalSource) ||
                HasTextSelection(e.OriginalSource))
                return;

            var hyperlink = FindAncestor<Hyperlink>(e.OriginalSource);
            if (hyperlink != null)
            {
                if (hyperlink.NavigateUri is Uri uri &&
                    (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps))
                {
                    Process.Start(new ProcessStartInfo(uri.AbsoluteUri) { UseShellExecute = true });
                }
                e.Handled = true;
                return;
            }

            if (_viewModel.IsDescriptionPreview)
            {
                _viewModel.ToggleDescriptionPreviewCommand.Execute(null);
                Dispatcher.BeginInvoke(() =>
                {
                    DescriptionTextBox.Focus();
                    DescriptionTextBox.CaretIndex = DescriptionTextBox.Text.Length;
                });
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
                      "- Ctrl + Shift + C : Kopyala\n" +
                      "- Ctrl + +/- / 0 (numpad da olur): Yazı boyutu";
            System.Windows.MessageBox.Show(msg, "Kısayollar", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }
}
