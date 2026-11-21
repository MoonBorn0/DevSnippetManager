using DevSnippetManager.ViewModels;
using KodDepolama;
using System;
using System.Windows;
using System.Windows.Input;

namespace DevSnippetManager.Views
{
    public partial class HistoryWindow : Window
    {
        private readonly HistoryViewModel _viewModel;
        private readonly Func<SnippetVersion, bool> _onRestore;

        public HistoryWindow(HistoryViewModel viewModel, Func<SnippetVersion, bool> onRestore)
        {
            InitializeComponent();
            _viewModel = viewModel;
            _onRestore = onRestore;
            DataContext = _viewModel;
        }

        private void Window_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                Close();
                e.Handled = true;
            }
            else if (e.Key == Key.Up && _viewModel.Versions.Count > 0)
            {
                var idx = _viewModel.SelectedVersion == null ? 0 : _viewModel.Versions.IndexOf(_viewModel.SelectedVersion);
                var next = Math.Max(0, idx - 1);
                _viewModel.SelectedVersion = _viewModel.Versions[next];
                VersionsListBox.ScrollIntoView(_viewModel.SelectedVersion);
                e.Handled = true;
            }
            else if (e.Key == Key.Down && _viewModel.Versions.Count > 0)
            {
                var idx = _viewModel.SelectedVersion == null ? -1 : _viewModel.Versions.IndexOf(_viewModel.SelectedVersion);
                var next = Math.Min(_viewModel.Versions.Count - 1, idx + 1);
                _viewModel.SelectedVersion = _viewModel.Versions[next];
                VersionsListBox.ScrollIntoView(_viewModel.SelectedVersion);
                e.Handled = true;
            }
        }

        private void RestoreButton_Click(object sender, RoutedEventArgs e)
        {
            if (_viewModel.SelectedVersion == null)
                return;

            if (_onRestore(_viewModel.SelectedVersion.Version))
            {
                Close();
            }
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
