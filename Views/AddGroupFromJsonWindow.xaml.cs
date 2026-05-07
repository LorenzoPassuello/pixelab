using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;

namespace Pixelab
{
    public partial class AddGroupFromJsonWindow : Window
    {
        private static LocalizationManager Loc => LocalizationManager.Instance;

        private readonly Func<List<PatternGenerator.ColorGroup>> _getGroups;

        public string GroupId { get; private set; } = "";
        public string GroupName { get; private set; } = "";
        public string JsonFilePath { get; private set; } = "";

        public AddGroupFromJsonWindow(Func<List<PatternGenerator.ColorGroup>> getGroups)
        {
            InitializeComponent();
            _getGroups = getGroups;
            Loaded += (_, _) => ApplyLocalization();
        }

        private void ApplyLocalization()
        {
            Title = Loc.T("add_group_from_json.title");
            DetailsHeader.Text = Loc.T("add_group_from_json.details");
            GroupIdLabel.Text = Loc.T("add_group_from_json.group_id");
            GroupIdHint.Text = Loc.T("add_group_from_json.group_id_hint");
            GroupNameLabel.Text = Loc.T("add_group_from_json.group_name");
            GroupNameHint.Text = Loc.T("add_group_from_json.group_name_hint");
            JsonFileLabel.Text = Loc.T("add_group_from_json.json_file");
            BrowseButton.Content = Loc.T("add_group_from_json.browse");
            CancelButton.Content = Loc.T("buttons.cancel");
            ConfirmButton.Content = Loc.T("add_group_from_json.confirm_button");
        }

        private void Browse_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                Filter = "JSON Files (*.json)|*.json|All Files (*.*)|*.*",
                Title = Loc.T("add_group_from_json.title")
            };

            if (dialog.ShowDialog() == true)
                JsonFilePathTextBox.Text = dialog.FileName;
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private void Confirm_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(GroupIdTextBox.Text))
            {
                MessageBox.Show(Loc.T("add_group_from_json.error_no_group_id"), Loc.T("settings.error"),
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            string groupId = GroupIdTextBox.Text.Trim();

            if (_getGroups().Any(g => g.GroupId == groupId))
            {
                MessageBox.Show(Loc.T("add_group_from_json.error_group_exists", groupId), Loc.T("settings.error"),
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (string.IsNullOrEmpty(JsonFilePathTextBox.Text))
            {
                MessageBox.Show(Loc.T("add_group_from_json.error_no_file"), Loc.T("settings.error"),
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            GroupId = groupId;
            GroupName = string.IsNullOrWhiteSpace(GroupNameTextBox.Text)
                ? GroupId
                : GroupNameTextBox.Text.Trim();
            JsonFilePath = JsonFilePathTextBox.Text;

            DialogResult = true;
            Close();
        }
    }
}
