using DevSnippetManager.ViewModels;
using System.Windows;

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

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
