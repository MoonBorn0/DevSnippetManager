using DevSnippetManager.ViewModels;
using DevSnippetManager.Services;
using System;
using System.ComponentModel;
using System.IO;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using Microsoft.Web.WebView2.Core;

namespace KodDepolama
{
    public partial class MainWindow : Window
    {
        [DllImport("user32.dll")]
        private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

        [DllImport("user32.dll")]
        private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

        private const int HOTKEY_ID = 9000;
        private const uint MOD_CONTROL = 0x0002;
        private const uint MOD_SHIFT = 0x0004;
        private const uint VK_S = 0x53; // 'S' key

        private MainViewModel _viewModel;
        private bool _editorReady;
        private string _pendingEditorCode = string.Empty;
        private string _pendingEditorLanguage = "csharp";

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

            InitializeAsync();
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
            RegisterHotKey(handle, HOTKEY_ID, MOD_CONTROL | MOD_SHIFT, VK_S);
        }

        private IntPtr HwndHook(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            const int WM_HOTKEY = 0x0312;
            if (msg == WM_HOTKEY && wParam.ToInt32() == HOTKEY_ID)
            {
                this.Show();
                this.WindowState = WindowState.Normal;
                this.Activate();
                _viewModel.NewSnippetCommand.Execute(null);
                handled = true;
            }
            return IntPtr.Zero;
        }

        protected override void OnClosing(CancelEventArgs e)
        {
            if (!_viewModel.ConfirmDiscardChanges())
            {
                e.Cancel = true;
                return;
            }
            base.OnClosing(e);
        }

        protected override void OnClosed(EventArgs e)
        {
            IntPtr handle = new WindowInteropHelper(this).Handle;
            UnregisterHotKey(handle, HOTKEY_ID);
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
        }

        private void BtnShortcuts_Click(object sender, RoutedEventArgs e)
        {
            var msg = "Global Kısayol: Ctrl + Shift + S (Uygulama kapalıyken bile yeni kod penceresini açar)\n\n" +
                      "Uygulama İçi Kısayollar:\n" +
                      "- Ctrl + S : Kaydet\n" +
                      "- Ctrl + N : Yeni\n" +
                      "- Ctrl + F : Arama\n" +
                      "- Ctrl + Shift + C : Kopyala";
            System.Windows.MessageBox.Show(msg, "Kısayollar", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }
}
