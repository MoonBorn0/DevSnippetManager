using DevSnippetManager.ViewModels;
using KodDepolama;
using System;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using WinForms = System.Windows.Forms;

namespace DevSnippetManager.Views
{
    public partial class QuickSearchWindow : Window
    {
        private const uint MONITOR_DEFAULTTONEAREST = 2;
        private const int MDT_EFFECTIVE_DPI = 0;

        [StructLayout(LayoutKind.Sequential)]
        private struct POINT
        {
            public int X;
            public int Y;
        }

        [DllImport("user32.dll")]
        private static extern IntPtr MonitorFromPoint(POINT pt, uint dwFlags);

        [DllImport("shcore.dll")]
        private static extern int GetDpiForMonitor(IntPtr hmonitor, int dpiType, out uint dpiX, out uint dpiY);

        private readonly QuickSearchViewModel _viewModel;
        private readonly Action<Snippet> _onOpenInMain;

        public bool SuppressDeactivateHide { get; set; }
        public bool AllowClose { get; set; }

        public QuickSearchWindow(MainViewModel mainViewModel, Action<Snippet> onOpenInMain)
        {
            InitializeComponent();
            _onOpenInMain = onOpenInMain;
            _viewModel = new QuickSearchViewModel(mainViewModel);
            DataContext = _viewModel;

            _viewModel.CloseRequested += (_, _) => Hide();
            _viewModel.OpenInMainRequested += (_, snippet) =>
            {
                Hide();
                _onOpenInMain(snippet);
            };
        }

        public void ShowAndFocus()
        {
            _viewModel.Reset();
            var cursor = WinForms.Cursor.Position;
            PositionOnScreen(cursor, usePresentationSource: false);
            Show();
            PositionOnScreen(cursor, usePresentationSource: true);
            Activate();
            Dispatcher.BeginInvoke(() =>
            {
                SearchTextBox.Focus();
                Keyboard.Focus(SearchTextBox);
                SearchTextBox.SelectAll();
            });
        }

        private void PositionOnScreen(System.Drawing.Point cursor, bool usePresentationSource)
        {
            var screen = WinForms.Screen.FromPoint(cursor);
            var workingArea = screen.WorkingArea;

            double areaLeft;
            double areaTop;
            double areaWidth;
            double areaHeight;

            var source = usePresentationSource ? PresentationSource.FromVisual(this) : null;
            if (source?.CompositionTarget != null)
            {
                var transform = source.CompositionTarget.TransformFromDevice;
                var topLeft = transform.Transform(new System.Windows.Point(workingArea.Left, workingArea.Top));
                var bottomRight = transform.Transform(new System.Windows.Point(workingArea.Right, workingArea.Bottom));
                areaLeft = topLeft.X;
                areaTop = topLeft.Y;
                areaWidth = bottomRight.X - topLeft.X;
                areaHeight = bottomRight.Y - topLeft.Y;
            }
            else
            {
                var (scaleX, scaleY) = GetMonitorDpiScale(cursor);
                areaLeft = workingArea.Left / scaleX;
                areaTop = workingArea.Top / scaleY;
                areaWidth = workingArea.Width / scaleX;
                areaHeight = workingArea.Height / scaleY;
            }

            var windowWidth = ActualWidth > 0 ? ActualWidth : Width;
            Left = areaLeft + Math.Max(0, (areaWidth - windowWidth) / 2.0);
            Top = areaTop + Math.Max(24, areaHeight / 6.0);
        }

        private (double ScaleX, double ScaleY) GetMonitorDpiScale(System.Drawing.Point cursor)
        {
            try
            {
                var monitor = MonitorFromPoint(new POINT { X = cursor.X, Y = cursor.Y }, MONITOR_DEFAULTTONEAREST);
                if (monitor != IntPtr.Zero && GetDpiForMonitor(monitor, MDT_EFFECTIVE_DPI, out var dpiX, out var dpiY) == 0 && dpiX > 0 && dpiY > 0)
                {
                    return (dpiX / 96.0, dpiY / 96.0);
                }
            }
            catch
            {
                // Fall back to WPF visual DPI
            }

            var dpi = VisualTreeHelper.GetDpi(this);
            return (dpi.DpiScaleX > 0 ? dpi.DpiScaleX : 1.0, dpi.DpiScaleY > 0 ? dpi.DpiScaleY : 1.0);
        }

        private void Window_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == Key.Up)
            {
                _viewModel.MoveSelection(-1);
                ScrollSelectedIntoView();
                e.Handled = true;
            }
            else if (e.Key == Key.Down)
            {
                _viewModel.MoveSelection(1);
                ScrollSelectedIntoView();
                e.Handled = true;
            }
            else if (e.Key == Key.Enter)
            {
                if ((Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control)
                {
                    _viewModel.OpenSelectedCommand.Execute(null);
                }
                else
                {
                    _viewModel.CopySelectedCommand.Execute(null);
                }
                e.Handled = true;
            }
            else if (e.Key == Key.Escape)
            {
                Hide();
                e.Handled = true;
            }
        }

        private void ScrollSelectedIntoView()
        {
            if (_viewModel.SelectedResult != null)
            {
                ResultsListBox.ScrollIntoView(_viewModel.SelectedResult);
            }
        }

        private void ResultsListBox_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (_viewModel.SelectedResult != null)
            {
                _viewModel.CopySelectedCommand.Execute(null);
            }
        }

        private void Window_Deactivated(object? sender, EventArgs e)
        {
            if (!SuppressDeactivateHide && IsVisible)
            {
                Hide();
            }
        }

        protected override void OnClosing(CancelEventArgs e)
        {
            if (!AllowClose)
            {
                e.Cancel = true;
                Hide();
                return;
            }
            base.OnClosing(e);
        }
    }
}
