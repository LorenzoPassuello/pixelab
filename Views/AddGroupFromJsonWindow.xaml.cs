using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Windows;

namespace Pixelab
{
    public partial class AddGroupFromJsonWindow : Window
    {
        private static LocalizationManager Loc => LocalizationManager.Instance;

        public string JsonFilePath { get; private set; } = "";

        private bool _formatVisible = false;

        private const string JsonSample =
            "{\n" +
            "  \"groups\": [\n" +
            "    {\n" +
            "      \"group_id\": \"my_group\",\n" +
            "      \"name\": \"My Group\",\n" +
            "      \"enabled\": true,\n" +
            "      \"colors\": [\n" +
            "        {\n" +
            "          \"color_id\": \"MG_001\",\n" +
            "          \"name\": \"Red\",\n" +
            "          \"hex\": \"#FF0000\",\n" +
            "          \"enabled\": true\n" +
            "        }\n" +
            "      ]\n" +
            "    }\n" +
            "  ]\n" +
            "}";

        public AddGroupFromJsonWindow()
        {
            InitializeComponent();
            FormatTextBox.Text = JsonSample;
        }

        private void Browse_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                Filter = "JSON Files (*.json)|*.json|All Files (*.*)|*.*",
                Title = "Select JSON File"
            };

            if (dialog.ShowDialog() != true) return;

            JsonFilePathTextBox.Text = dialog.FileName;
            JsonFilePath = dialog.FileName;
            UpdatePreview(dialog.FileName);
        }

        private void UpdatePreview(string filePath)
        {
            try
            {
                string json = File.ReadAllText(filePath);
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;

                int colorCount = 0, groupCount = 0;

                if (root.TryGetProperty("groups", out var groups))
                {
                    foreach (var g in groups.EnumerateArray())
                    {
                        groupCount++;
                        if (g.TryGetProperty("colors", out var colors))
                            colorCount += colors.GetArrayLength();
                    }
                }
                else if (root.TryGetProperty("colors", out var flat))
                {
                    var colorList = flat.EnumerateArray().ToList();
                    colorCount = colorList.Count;
                    groupCount = colorList
                        .Select(c => c.TryGetProperty("group", out var gp) ? gp.GetString() ?? "custom" : "custom")
                        .Distinct()
                        .Count();
                }

                if (colorCount == 0 && groupCount == 0)
                    PreviewText.Text = "No colors or groups found in this file.";
                else
                    PreviewText.Text = $"{colorCount} color(s) from {groupCount} group(s)";

                PreviewText.Foreground = colorCount > 0
                    ? System.Windows.Media.Brushes.LightGreen
                    : System.Windows.Media.Brushes.OrangeRed;
            }
            catch
            {
                PreviewText.Text = "Could not parse file — invalid JSON.";
                PreviewText.Foreground = System.Windows.Media.Brushes.OrangeRed;
            }
        }

        private void ToggleFormat_Click(object sender, RoutedEventArgs e)
        {
            _formatVisible = !_formatVisible;
            FormatTextBox.Visibility = _formatVisible ? Visibility.Visible : Visibility.Collapsed;
            ToggleFormatButton.Content = _formatVisible
                ? "▼  Hide expected JSON format"
                : "▶  Show expected JSON format";
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private void Confirm_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(JsonFilePath) || !File.Exists(JsonFilePath))
            {
                MessageBox.Show("Please select a valid JSON file.", Loc.T("settings.error"),
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            DialogResult = true;
            Close();
        }
    }
}
