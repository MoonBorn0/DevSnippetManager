using DevSnippetManager.Services;
using System.Windows;

namespace DevSnippetManager.Views
{
    public partial class ImportOptionsWindow : Window
    {
        public ImportMode SelectedMode { get; private set; } = ImportMode.Merge;

        public ImportOptionsWindow(int snippetCount)
        {
            InitializeComponent();
            CountText.Text = $"Dosyada {snippetCount} kod parçacığı bulundu. İçe aktarma yöntemini seçin:";
        }

        private void ImportButton_Click(object sender, RoutedEventArgs e)
        {
            if (SkipRadio.IsChecked == true)
                SelectedMode = ImportMode.SkipDuplicates;
            else if (ReplaceRadio.IsChecked == true)
                SelectedMode = ImportMode.ReplaceAll;
            else
                SelectedMode = ImportMode.Merge;

            DialogResult = true;
            Close();
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}
