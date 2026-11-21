using DevSnippetManager.ViewModels;
using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Navigation;

namespace DevSnippetManager.Views
{
    public partial class SettingsWindow : Window
    {
        private readonly MainViewModel _viewModel;

        public SettingsWindow(MainViewModel viewModel)
        {
            InitializeComponent();
            _viewModel = viewModel;
            DataContext = _viewModel;
        }

        private void MinimizeToTrayToggle_Click(object sender, RoutedEventArgs e)
        {
            _viewModel.SaveSettings();
        }

        private async void SaveTokenButton_Click(object sender, RoutedEventArgs e)
        {
            var token = GitHubTokenBox.Password;
            var saved = await _viewModel.Sync.SaveGitHubTokenAsync(token);
            if (saved)
            {
                GitHubTokenBox.Clear();
            }
        }

        private void TokenLink_RequestNavigate(object sender, RequestNavigateEventArgs e)
        {
            try
            {
                Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri) { UseShellExecute = true });
            }
            catch (Exception)
            {
                // Ignore browser launch failures
            }
            e.Handled = true;
        }

        private void CopyGistIdButton_Click(object sender, RoutedEventArgs e)
        {
            var id = _viewModel.Sync.GistId;
            if (string.IsNullOrWhiteSpace(id))
                return;

            try
            {
                System.Windows.Clipboard.SetText(id);
                _viewModel.MessageQueue.Enqueue("Gist ID panoya kopyalandı");
            }
            catch (Exception)
            {
                // Ignore clipboard lock errors
            }
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
