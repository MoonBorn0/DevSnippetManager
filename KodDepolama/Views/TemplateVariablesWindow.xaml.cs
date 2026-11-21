using DevSnippetManager.Services;
using MaterialDesignThemes.Wpf;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;

namespace DevSnippetManager.Views
{
    public partial class TemplateVariablesWindow : Window
    {
        private readonly List<(string Name, System.Windows.Controls.TextBox Input)> _inputs = new();

        public IReadOnlyDictionary<string, string>? ResultValues { get; private set; }

        public TemplateVariablesWindow(IReadOnlyList<TemplateVariable> variables)
        {
            InitializeComponent();

            var style = TryFindResource("MaterialDesignFloatingHintTextBox") as Style;
            foreach (var variable in variables)
            {
                var textBox = new System.Windows.Controls.TextBox
                {
                    Text = variable.DefaultValue ?? string.Empty,
                    Margin = new Thickness(0, 0, 0, 12)
                };

                if (style != null)
                {
                    textBox.Style = style;
                }

                HintAssist.SetHint(textBox, variable.Name);
                FieldsPanel.Children.Add(textBox);
                _inputs.Add((variable.Name, textBox));
            }
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            if (_inputs.Count > 0)
            {
                var first = _inputs[0].Input;
                first.Focus();
                first.SelectAll();
            }
        }

        private void CopyButton_Click(object sender, RoutedEventArgs e)
        {
            var values = new Dictionary<string, string>(_inputs.Count);
            foreach (var (name, input) in _inputs)
            {
                values[name] = input.Text ?? string.Empty;
            }

            ResultValues = values;
            DialogResult = true;
            Close();
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            ResultValues = null;
            DialogResult = false;
            Close();
        }
    }
}
