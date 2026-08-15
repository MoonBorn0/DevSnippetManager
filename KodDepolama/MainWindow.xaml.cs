using DevSnippetManager.ViewModels;
using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;

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

        public MainWindow()
        {
            InitializeComponent();
            _viewModel = (MainViewModel)DataContext;

            // Setup WebView2 and Monaco bridge
            _viewModel.UpdateEditorCodeAction = (code) =>
            {
                if (webView.CoreWebView2 != null)
                {
                    var encoded = JsonSerializer.Serialize(code ?? string.Empty);
                    webView.CoreWebView2.ExecuteScriptAsync($"window.editor.setValue({encoded});");
                }
            };

            _viewModel.GetEditorCodeFunc = async () =>
            {
                if (webView.CoreWebView2 != null)
                {
                    var json = await webView.CoreWebView2.ExecuteScriptAsync("window.editor.getValue();");
                    return JsonSerializer.Deserialize<string>(json) ?? string.Empty;
                }
                return string.Empty;
            };

            InitializeAsync();
        }

        private async void InitializeAsync()
        {
            await webView.EnsureCoreWebView2Async();
            var htmlPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "MonacoEditor.html");
            if (File.Exists(htmlPath))
            {
                webView.Source = new Uri(htmlPath);
            }
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
        }

        private void BtnShortcuts_Click(object sender, RoutedEventArgs e)
        {
            var msg = "Global Kısayol: Ctrl + Shift + S (Uygulama kapalıyken bile yeni kod penceresini açar)\n\n" +
                      "Uygulama İçi Kısayollar:\n" +
                      "- Ctrl + S : Kaydet\n" +
                      "- Ctrl + N : Yeni\n" +
                      "- Ctrl + F : Arama";
            System.Windows.MessageBox.Show(msg, "Kısayollar", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }
}
