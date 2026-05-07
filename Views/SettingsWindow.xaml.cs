// ============================================================================
// SettingsWindow.xaml.cs
// Settings dialog window for configuring application appearance and behavior.
// Includes: Canvas settings, Grid settings, Language selection, Color Manager.
// ============================================================================

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace Pixelab
{
    /// <summary>
    /// Settings dialog window that allows users to configure:
    /// - Canvas appearance (background color)
    /// - Grid overlay (color, opacity)
    /// - Highlight appearance (color, opacity)
    /// - Language selection
    /// - Color manager (enable/disable bead colors and groups)
    /// </summary>
    public partial class SettingsWindow : Window
    {
        // ============================================================================
        // PRIVATE FIELDS
        // ============================================================================
        
        /// <summary>Flag to prevent event handling during initialization.</summary>
        private bool _isInitialized = false;
        
        /// <summary>Path to the colors database file.</summary>
        private string _colorsPath = "";
        
        /// <summary>List of all bead colors loaded from the database.</summary>
        private List<ColorData> _colors = new();
        
        /// <summary>List of all color groups loaded from the database.</summary>
        private List<GroupData> _groups = new();
        
        /// <summary>Shortcut reference to the localization manager.</summary>
        private static LocalizationManager Loc => LocalizationManager.Instance;
        
        /// <summary>Currently selected language code.</summary>
        private string _selectedLanguage = "en";
        
        /// <summary>Currently selected color space.</summary>
        private PatternGenerator.ColorSpace _selectedColorSpace = PatternGenerator.ColorSpace.Lab;

        // ============================================================================
        // PUBLIC PROPERTIES
        // ============================================================================
        
        /// <summary>Canvas background color setting.</summary>
        public Color CanvasColor { get; private set; } = Color.FromRgb(37, 37, 38);
        
        /// <summary>Grid line color setting (without alpha).</summary>
        public Color GridColor { get; private set; } = Color.FromRgb(128, 128, 128);
        
        /// <summary>Highlight color setting (without alpha).</summary>
        public Color HighlightColor { get; private set; } = Color.FromRgb(255, 255, 0);
        
        /// <summary>Accent color for UI elements.</summary>
        public Color AccentColor { get; private set; } = Color.FromRgb(183, 0, 116);
        
        /// <summary>Grid line opacity (0-255).</summary>
        public byte GridOpacity { get; private set; } = 150;
        
        /// <summary>Highlight opacity (0-255).</summary>
        public byte HighlightOpacity { get; private set; } = 200;

        // ============================================================================
        // CONSTRUCTOR
        // ============================================================================
        
        /// <summary>
        /// Initializes the settings window.
        /// </summary>
        public SettingsWindow()
        {
            InitializeComponent();
            _isInitialized = true;
            
            // Register slider event handlers
            GridOpacitySlider.ValueChanged += GridOpacitySlider_ValueChanged;
            HighlightOpacitySlider.ValueChanged += HighlightOpacitySlider_ValueChanged;
            
            // Colors path will be set from MainWindow in Window_Loaded
        }

        // ============================================================================
        // WINDOW LIFECYCLE
        // ============================================================================
        
        /// <summary>
        /// Handles window loaded event - initializes all settings from MainWindow.
        /// </summary>
        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            // Apply localization first
            ApplyLocalization();
            
            CategoriesListBox.SelectedIndex = 0;
            
            if (Owner is MainWindow mainWindow)
            {
                // Get colors path from MainWindow
                _colorsPath = mainWindow.GetColorsPath();
                
                var settings = mainWindow.GetCurrentSettings();
                CanvasColor = settings.canvasColor;
                GridColor = Color.FromRgb(settings.gridColor.R, settings.gridColor.G, settings.gridColor.B);
                GridOpacity = settings.gridColor.A;
                HighlightColor = Color.FromRgb(settings.highlightColor.R, settings.highlightColor.G, settings.highlightColor.B);
                HighlightOpacity = settings.highlightColor.A;
                AccentColor = settings.accentColor;
                _selectedLanguage = mainWindow.GetCurrentLanguage();
                _selectedColorSpace = settings.colorSpace;
                
                // Set compression combo box
                var compression = mainWindow.GetCompressionLevel();
                for (int i = 0; i < CompressionComboBox.Items.Count; i++)
                {
                    if (CompressionComboBox.Items[i] is ComboBoxItem item && 
                        item.Tag?.ToString() == ((int)compression).ToString())
                    {
                        CompressionComboBox.SelectedIndex = i;
                        break;
                    }
                }
                
                // Set color space combo box
                ColorSpaceComboBox.SelectedIndex = (int)_selectedColorSpace;
                
                UpdateUI();
            }
            
            LoadColorsData();
            LoadLanguages();
        }

        /// <summary>
        /// Applies localized strings to all UI elements in the Settings window.
        /// </summary>
        private void ApplyLocalization()
        {
            // Window title
            Title = Loc.T("settings.title");
            
            // Sidebar header and menu items
            SettingsHeader.Text = Loc.T("settings.title").ToUpper();
            CanvasMenuItem.Content = $"🖼  {Loc.T("settings.canvas")}";
            LanguageMenuItem.Content = $"🌐  {Loc.T("settings.language")}";
            ColorManagerMenuItem.Content = $"🎨  {Loc.T("settings.color_manager")}";
            
            // Canvas Panel - Appearance Section
            AppearanceSectionHeader.Text = Loc.T("settings.appearance");
            AccentColorLabel.Text = Loc.T("settings.accent_color");
            AccentColorDesc.Text = Loc.T("settings.accent_color_desc");
            
            // Canvas Panel - Canvas Section
            CanvasSectionHeader.Text = Loc.T("settings.canvas_background");
            BackgroundColorLabel.Text = Loc.T("settings.background_color");
            GridSectionHeader.Text = Loc.T("settings.grid");
            GridColorLabel.Text = Loc.T("settings.grid_color");
            GridOpacityLabel.Text = Loc.T("settings.grid_opacity");
            HighlightSectionHeader.Text = Loc.T("settings.highlight");
            HighlightColorLabel.Text = Loc.T("settings.highlight_color");
            HighlightOpacityLabel.Text = Loc.T("settings.highlight_opacity");
            
            // Language Panel
            LanguageSectionHeader.Text = Loc.T("settings.language").ToUpper();
            SelectLanguageLabel.Text = Loc.T("settings.select_language");
            LanguageRestartNote.Text = Loc.T("settings.language_restart");
            AvailableLanguagesHeader.Text = Loc.T("settings.available_languages");
            
            // Color Manager Panel
            PatternGenerationHeader.Text = Loc.T("settings.pattern_generation");
            ColorSpaceLabel.Text = Loc.T("settings.color_space");
            ColorSpaceRGB.Content = Loc.T("settings.color_space_rgb");
            ColorSpaceLab.Content = Loc.T("settings.color_space_lab");
            ColorSpaceDescription.Text = Loc.T("settings.color_space_desc");
            ColorCompressionLabel.Text = Loc.T("settings.color_compression");
            CompressionOff.Content = Loc.T("settings.compression_off");
            CompressionLow.Content = Loc.T("settings.compression_low");
            CompressionMedium.Content = Loc.T("settings.compression_medium");
            CompressionHigh.Content = Loc.T("settings.compression_high");
            CompressionDescription.Text = Loc.T("settings.compression_desc");
            ColorGroupsHeader.Text = Loc.T("settings.color_groups");
            ColorGroupsDescription.Text = Loc.T("settings.color_groups_desc");
            IndividualColorsHeader.Text = Loc.T("settings.individual_colors");
            IndividualColorsDescription.Text = Loc.T("settings.individual_colors_desc");
            AllGroupsItem.Content = Loc.T("settings.all_groups");
            
            // Buttons
            CancelButton.Content = Loc.T("buttons.cancel");
            SaveButton.Content = Loc.T("buttons.save");

            // Custom Colors
            ImportColorsButton.Content = Loc.T("settings.import_json");
            ImportColorsHint.Text = Loc.T("settings.import_colors_hint");
        }

        private void LoadLanguages()
        {
            var languages = Loc.GetAvailableLanguages();
            
            LanguageComboBox.Items.Clear();
            AvailableLanguagesPanel.Children.Clear();
            
            int selectedIndex = 0;
            int i = 0;
            
            foreach (var lang in languages)
            {
                LanguageComboBox.Items.Add(new ComboBoxItem { Content = lang.Name, Tag = lang.Code });
                
                if (lang.Code == _selectedLanguage)
                    selectedIndex = i;
                
                // Add to available languages list
                var tb = new TextBlock
                {
                    Text = $"• {lang.Name} ({lang.Code})",
                    Foreground = new SolidColorBrush(Color.FromRgb(170, 170, 170)),
                    FontSize = 12,
                    Margin = new Thickness(0, 0, 0, 4)
                };
                AvailableLanguagesPanel.Children.Add(tb);
                
                i++;
            }
            
            if (LanguageComboBox.Items.Count > 0)
                LanguageComboBox.SelectedIndex = selectedIndex;
        }

        private void LanguageComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (LanguageComboBox.SelectedItem is ComboBoxItem item && item.Tag is string code)
            {
                _selectedLanguage = code;
            }
        }

        private void UpdateUI()
        {
            _isInitialized = false;
            
            AccentColorPreview.Background = new SolidColorBrush(AccentColor);
            AccentColorTextBox.Text = $"#{AccentColor.R:X2}{AccentColor.G:X2}{AccentColor.B:X2}";
            
            CanvasColorPreview.Background = new SolidColorBrush(CanvasColor);
            CanvasColorTextBox.Text = $"#{CanvasColor.R:X2}{CanvasColor.G:X2}{CanvasColor.B:X2}";
            
            GridColorPreview.Background = new SolidColorBrush(GridColor);
            GridColorTextBox.Text = $"#{GridColor.R:X2}{GridColor.G:X2}{GridColor.B:X2}";
            GridOpacitySlider.Value = GridOpacity;
            GridOpacityText.Text = GridOpacity.ToString();
            
            HighlightColorPreview.Background = new SolidColorBrush(HighlightColor);
            HighlightColorTextBox.Text = $"#{HighlightColor.R:X2}{HighlightColor.G:X2}{HighlightColor.B:X2}";
            HighlightOpacitySlider.Value = HighlightOpacity;
            HighlightOpacityText.Text = HighlightOpacity.ToString();
            
            _isInitialized = true;
        }

        #region Color Manager

        private void LoadColorsData()
        {
            try
            {
                if (!File.Exists(_colorsPath)) return;

                string json = File.ReadAllText(_colorsPath);
                using var doc = JsonDocument.Parse(json);

                _colors.Clear();
                _groups.Clear();

                if (doc.RootElement.TryGetProperty("groups", out var groupsArr))
                {
                    foreach (var g in groupsArr.EnumerateArray())
                    {
                        string groupId = g.GetProperty("group_id").GetString() ?? "";
                        _groups.Add(new GroupData
                        {
                            GroupId = groupId,
                            Name    = g.GetProperty("name").GetString() ?? "",
                            Enabled = g.GetProperty("enabled").GetBoolean()
                        });

                        if (g.TryGetProperty("colors", out var colorsArr))
                        {
                            foreach (var c in colorsArr.EnumerateArray())
                            {
                                string hex = c.GetProperty("hex").GetString() ?? "#000000";
                                (byte r, byte gByte, byte b) = HexToRgb(hex);
                                _colors.Add(new ColorData
                                {
                                    ColorId  = c.GetProperty("color_id").GetString() ?? "",
                                    Name     = c.GetProperty("name").GetString() ?? "",
                                    R = r, G = gByte, B = b,
                                    Group    = groupId,
                                    Enabled  = c.GetProperty("enabled").GetBoolean(),
                                    Favorite = c.GetProperty("favorite").GetBoolean()
                                });
                            }
                        }
                    }
                }

                PopulateColorGroups();
                PopulateGroupFilter();
                PopulateColors("all");
            }
            catch { }
        }

        private static (byte r, byte g, byte b) HexToRgb(string hex)
        {
            hex = hex.TrimStart('#');
            return (Convert.ToByte(hex[0..2], 16), Convert.ToByte(hex[2..4], 16), Convert.ToByte(hex[4..6], 16));
        }

        private void PopulateColorGroups()
        {
            ColorGroupsPanel.Children.Clear();
            
            foreach (var group in _groups)
            {
                var colorCount = _colors.Count(c => c.Group == group.GroupId);
                var enabledCount = _colors.Count(c => c.Group == group.GroupId && c.Enabled);
                
                var cb = new CheckBox
                {
                    Content = $"{group.Name} ({enabledCount}/{colorCount} enabled)",
                    Tag = group.GroupId,
                    Foreground = Brushes.White,
                    Margin = new Thickness(0, 0, 0, 8)
                };
                
                // Set checkbox state based on enabled colors
                // Indeterminate is set only programmatically, not via IsThreeState
                if (enabledCount == 0)
                    cb.IsChecked = false;
                else if (enabledCount == colorCount)
                    cb.IsChecked = true;
                else
                    cb.IsChecked = null; // Indeterminate - some colors enabled
                
                cb.Checked += GroupCheckBox_Changed;
                cb.Unchecked += GroupCheckBox_Changed;
                ColorGroupsPanel.Children.Add(cb);
            }
        }

        private void GroupCheckBox_Changed(object sender, RoutedEventArgs e)
        {
            if (sender is CheckBox cb && cb.Tag is string groupId)
            {
                var group = _groups.FirstOrDefault(g => g.GroupId == groupId);
                if (group != null)
                {
                    bool enable = cb.IsChecked == true;
                    group.Enabled = enable;
                    
                    // Enable/disable all colors in this group
                    foreach (var color in _colors.Where(c => c.Group == groupId))
                    {
                        color.Enabled = enable;
                    }
                    
                    // Update the checkbox text
                    var colorCount = _colors.Count(c => c.Group == groupId);
                    var enabledCount = _colors.Count(c => c.Group == groupId && c.Enabled);
                    cb.Content = $"{group.Name} ({enabledCount}/{colorCount} enabled)";
                    
                    // Refresh colors display
                    var currentFilter = (ColorGroupFilterComboBox.SelectedItem as ComboBoxItem)?.Tag as string ?? "all";
                    PopulateColors(currentFilter);
                }
            }
        }

        private void LoadColorGroups()
        {
            LoadColorsData();
            PopulateColorGroups();
            PopulateGroupFilter();
        }

        private void PopulateGroupFilter()
        {
            ColorGroupFilterComboBox.Items.Clear();
            ColorGroupFilterComboBox.Items.Add(new ComboBoxItem { Content = "All Groups", Tag = "all" });
            
            foreach (var group in _groups)
            {
                ColorGroupFilterComboBox.Items.Add(new ComboBoxItem { Content = group.Name, Tag = group.GroupId });
            }
            
            ColorGroupFilterComboBox.SelectedIndex = 0;
        }

        private void ColorGroupFilter_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ColorGroupFilterComboBox.SelectedItem is ComboBoxItem item && item.Tag is string groupId)
            {
                PopulateColors(groupId);
            }
        }

        private void PopulateColors(string groupFilter)
        {
            ColorsPanel.Children.Clear();
            
            var groupsToShow = groupFilter == "all" 
                ? _groups 
                : _groups.Where(g => g.GroupId == groupFilter).ToList();
            
            foreach (var group in groupsToShow)
            {
                var groupColors = _colors.Where(c => c.Group == group.GroupId).ToList();
                if (groupColors.Count == 0) continue;
                
                // Add group header
                var header = new TextBlock
                {
                    Text = group.Name.ToUpper(),
                    Foreground = new SolidColorBrush(Color.FromRgb(183, 0, 116)),
                    FontSize = 11,
                    FontWeight = FontWeights.Bold,
                    Margin = new Thickness(0, 8, 0, 6)
                };
                ColorsPanel.Children.Add(header);
                
                // Add colors in wrap panel
                var wrapPanel = new WrapPanel { Margin = new Thickness(0, 0, 0, 8) };
                
                foreach (var color in groupColors)
                {
                    var border = new Border
                    {
                        Width = 28, Height = 28,
                        Margin = new Thickness(2),
                        Background = new SolidColorBrush(Color.FromRgb(color.R, color.G, color.B)),
                        BorderThickness = new Thickness(2),
                        BorderBrush = color.Favorite ? Brushes.Gold : (color.Enabled ? Brushes.White : Brushes.DarkGray),
                        CornerRadius = new CornerRadius(4),
                        Opacity = color.Enabled ? 1.0 : 0.4,
                        Tag = color.ColorId,
                        Cursor = Cursors.Hand,
                        ToolTip = $"{color.ColorId}: {color.Name}\n{(color.Enabled ? "Enabled" : "Disabled")}{(color.Favorite ? " ★ Favorite" : "")}"
                    };
                    
                    border.MouseLeftButtonDown += ColorBorder_Click;
                    border.MouseRightButtonDown += ColorBorder_RightClick;
                    wrapPanel.Children.Add(border);
                }
                
                ColorsPanel.Children.Add(wrapPanel);
            }
            
            var allFiltered = groupFilter == "all" ? _colors : _colors.Where(c => c.Group == groupFilter).ToList();
            var enabledCount = allFiltered.Count(c => c.Enabled);
            ColorCountText.Text = $"{enabledCount} of {allFiltered.Count} colors enabled";
        }

        private void ColorBorder_Click(object sender, MouseButtonEventArgs e)
        {
            if (sender is Border border && border.Tag is string colorId)
            {
                var color = _colors.FirstOrDefault(c => c.ColorId == colorId);
                if (color != null)
                {
                    color.Enabled = !color.Enabled;
                    border.Opacity = color.Enabled ? 1.0 : 0.4;
                    border.BorderBrush = color.Favorite ? Brushes.Gold : (color.Enabled ? Brushes.White : Brushes.DarkGray);
                    border.ToolTip = $"{color.ColorId}: {color.Name}\n{(color.Enabled ? "Enabled" : "Disabled")}{(color.Favorite ? " ★ Favorite" : "")}";
                    
                    UpdateColorCounts();
                }
            }
        }

        private void ColorBorder_RightClick(object sender, MouseButtonEventArgs e)
        {
            if (sender is Border border && border.Tag is string colorId)
            {
                var color = _colors.FirstOrDefault(c => c.ColorId == colorId);
                if (color != null)
                {
                    color.Favorite = !color.Favorite;
                    border.BorderBrush = color.Favorite ? Brushes.Gold : (color.Enabled ? Brushes.White : Brushes.DarkGray);
                    border.ToolTip = $"{color.ColorId}: {color.Name}\n{(color.Enabled ? "Enabled" : "Disabled")}{(color.Favorite ? " ★ Favorite" : "")}";
                }
            }
        }

        private void UpdateColorCounts()
        {
            var groupFilter = (ColorGroupFilterComboBox.SelectedItem as ComboBoxItem)?.Tag as string ?? "all";
            var filtered = groupFilter == "all" ? _colors : _colors.Where(c => c.Group == groupFilter).ToList();
            var enabledCount = filtered.Count(c => c.Enabled);
            ColorCountText.Text = $"{enabledCount} of {filtered.Count} colors enabled";
            
            // Update group checkboxes state
            foreach (var child in ColorGroupsPanel.Children)
            {
                if (child is CheckBox cb && cb.Tag is string groupId)
                {
                    var group = _groups.FirstOrDefault(g => g.GroupId == groupId);
                    if (group != null)
                    {
                        var colorCount = _colors.Count(c => c.Group == groupId);
                        var groupEnabled = _colors.Count(c => c.Group == groupId && c.Enabled);
                        cb.Content = $"{group.Name} ({groupEnabled}/{colorCount} enabled)";
                        
                        // Temporarily remove event handlers to avoid recursion
                        cb.Checked -= GroupCheckBox_Changed;
                        cb.Unchecked -= GroupCheckBox_Changed;
                        
                        // Update checkbox state (Indeterminate set programmatically only)
                        if (groupEnabled == 0)
                            cb.IsChecked = false;
                        else if (groupEnabled == colorCount)
                            cb.IsChecked = true;
                        else
                            cb.IsChecked = null; // Indeterminate
                        
                        // Re-attach event handlers
                        cb.Checked += GroupCheckBox_Changed;
                        cb.Unchecked += GroupCheckBox_Changed;
                        
                        // Update group enabled state based on at least one color being enabled
                        group.Enabled = groupEnabled > 0;
                    }
                }
            }
        }

        private void SaveColorsData()
        {
            try
            {
                if (!File.Exists(_colorsPath)) return;

                string json = File.ReadAllText(_colorsPath);
                using var doc = JsonDocument.Parse(json);

                if (!doc.RootElement.TryGetProperty("groups", out var existingGroups)) return;

                var groupsArr = new List<Dictionary<string, object>>();
                foreach (var g in existingGroups.EnumerateArray())
                {
                    string groupId   = g.GetProperty("group_id").GetString() ?? "";
                    var groupMeta    = _groups.FirstOrDefault(x => x.GroupId == groupId);

                    var colorsArr = new List<Dictionary<string, object>>();
                    if (g.TryGetProperty("colors", out var existingColors))
                    {
                        foreach (var c in existingColors.EnumerateArray())
                        {
                            string colorId = c.GetProperty("color_id").GetString() ?? "";
                            var colorData  = _colors.FirstOrDefault(x => x.ColorId == colorId);
                            var colorDict  = JsonSerializer.Deserialize<Dictionary<string, object>>(c.GetRawText())!;
                            if (colorData != null)
                            {
                                colorDict["enabled"]  = colorData.Enabled;
                                colorDict["favorite"] = colorData.Favorite;
                            }
                            colorsArr.Add(colorDict);
                        }
                    }

                    groupsArr.Add(new Dictionary<string, object>
                    {
                        ["group_id"] = groupId,
                        ["name"]     = g.GetProperty("name").GetString() ?? "",
                        ["enabled"]  = (object)(groupMeta?.Enabled ?? g.GetProperty("enabled").GetBoolean()),
                        ["colors"]   = colorsArr
                    });
                }

                var root    = new Dictionary<string, object> { ["groups"] = groupsArr };
                var options = new JsonSerializerOptions { WriteIndented = true };
                File.WriteAllText(_colorsPath, JsonSerializer.Serialize(root, options));
            }
            catch { }
        }

        private class ColorData
        {
            public string ColorId { get; set; } = "";
            public string Name { get; set; } = "";
            public byte R { get; set; }
            public byte G { get; set; }
            public byte B { get; set; }
            public string Group { get; set; } = "";
            public bool Enabled { get; set; }
            public bool Favorite { get; set; }
        }

        private class GroupData
        {
            public string GroupId { get; set; } = "";
            public string Name { get; set; } = "";
            public bool Enabled { get; set; }
        }

        #endregion

        #region Slider Click Support

        private void Slider_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (sender is Slider slider)
            {
                var pos = e.GetPosition(slider);
                double ratio = pos.X / slider.ActualWidth;
                slider.Value = slider.Minimum + (ratio * (slider.Maximum - slider.Minimum));
            }
        }

        #endregion

        #region Navigation

        private void CategoriesListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!_isInitialized) return;
            if (CanvasPanel == null || LanguagePanel == null || ColorManagerPanel == null) return;
            
            if (CategoriesListBox.SelectedItem is ListBoxItem selectedItem)
            {
                string? tag = selectedItem.Tag as string;
                
                CanvasPanel.Visibility = Visibility.Collapsed;
                LanguagePanel.Visibility = Visibility.Collapsed;
                ColorManagerPanel.Visibility = Visibility.Collapsed;
                
                switch (tag)
                {
                    case "Canvas":
                        CanvasPanel.Visibility = Visibility.Visible;
                        CategoryTitle.Text = Loc.T("settings.canvas");
                        break;
                    case "Language":
                        LanguagePanel.Visibility = Visibility.Visible;
                        CategoryTitle.Text = Loc.T("settings.language");
                        break;
                    case "ColorManager":
                        ColorManagerPanel.Visibility = Visibility.Visible;
                        CategoryTitle.Text = Loc.T("settings.color_manager");
                        break;
                }
            }
        }

        #endregion

        #region Color Pickers

        private void AccentColorPreview_Click(object sender, MouseButtonEventArgs e)
        {
            var picker = new ColorPickerWindow(AccentColor) { Owner = this };
            if (picker.ShowDialog() == true)
            {
                AccentColor = picker.SelectedColor;
                AccentColorPreview.Background = new SolidColorBrush(AccentColor);
                _isInitialized = false;
                AccentColorTextBox.Text = $"#{AccentColor.R:X2}{AccentColor.G:X2}{AccentColor.B:X2}";
                _isInitialized = true;
            }
        }

        private void CanvasColorPreview_Click(object sender, MouseButtonEventArgs e)
        {
            var picker = new ColorPickerWindow(CanvasColor) { Owner = this };
            if (picker.ShowDialog() == true)
            {
                CanvasColor = picker.SelectedColor;
                CanvasColorPreview.Background = new SolidColorBrush(CanvasColor);
                _isInitialized = false;
                CanvasColorTextBox.Text = $"#{CanvasColor.R:X2}{CanvasColor.G:X2}{CanvasColor.B:X2}";
                _isInitialized = true;
            }
        }

        private void GridColorPreview_Click(object sender, MouseButtonEventArgs e)
        {
            var picker = new ColorPickerWindow(GridColor) { Owner = this };
            if (picker.ShowDialog() == true)
            {
                GridColor = picker.SelectedColor;
                GridColorPreview.Background = new SolidColorBrush(GridColor);
                _isInitialized = false;
                GridColorTextBox.Text = $"#{GridColor.R:X2}{GridColor.G:X2}{GridColor.B:X2}";
                _isInitialized = true;
            }
        }

        private void HighlightColorPreview_Click(object sender, MouseButtonEventArgs e)
        {
            var picker = new ColorPickerWindow(HighlightColor) { Owner = this };
            if (picker.ShowDialog() == true)
            {
                HighlightColor = picker.SelectedColor;
                HighlightColorPreview.Background = new SolidColorBrush(HighlightColor);
                _isInitialized = false;
                HighlightColorTextBox.Text = $"#{HighlightColor.R:X2}{HighlightColor.G:X2}{HighlightColor.B:X2}";
                _isInitialized = true;
            }
        }

        private void AccentColorTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (!_isInitialized) return;
            if (TryParseHexColor(AccentColorTextBox.Text, out Color color))
            {
                AccentColor = color;
                AccentColorPreview.Background = new SolidColorBrush(color);
            }
        }

        private void CanvasColorTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (!_isInitialized) return;
            if (TryParseHexColor(CanvasColorTextBox.Text, out Color color))
            {
                CanvasColor = color;
                CanvasColorPreview.Background = new SolidColorBrush(color);
            }
        }

        private void GridColorTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (!_isInitialized) return;
            if (TryParseHexColor(GridColorTextBox.Text, out Color color))
            {
                GridColor = color;
                GridColorPreview.Background = new SolidColorBrush(color);
            }
        }

        private void HighlightColorTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (!_isInitialized) return;
            if (TryParseHexColor(HighlightColorTextBox.Text, out Color color))
            {
                HighlightColor = color;
                HighlightColorPreview.Background = new SolidColorBrush(color);
            }
        }
        
        // Default color button handlers
        private void AccentColorDefault_Click(object sender, RoutedEventArgs e)
        {
            AccentColor = Color.FromRgb(183, 0, 116); // #B70074
            AccentColorPreview.Background = new SolidColorBrush(AccentColor);
            _isInitialized = false;
            AccentColorTextBox.Text = "#B70074";
            _isInitialized = true;
        }
        
        private void CanvasColorDefault_Click(object sender, RoutedEventArgs e)
        {
            CanvasColor = Color.FromRgb(255, 255, 255); // #FFFFFF
            CanvasColorPreview.Background = new SolidColorBrush(CanvasColor);
            _isInitialized = false;
            CanvasColorTextBox.Text = "#FFFFFF";
            _isInitialized = true;
        }
        
        private void GridColorDefault_Click(object sender, RoutedEventArgs e)
        {
            GridColor = Color.FromRgb(128, 128, 128); // #808080
            GridColorPreview.Background = new SolidColorBrush(GridColor);
            _isInitialized = false;
            GridColorTextBox.Text = "#808080";
            _isInitialized = true;
        }
        
        private void HighlightColorDefault_Click(object sender, RoutedEventArgs e)
        {
            HighlightColor = Color.FromRgb(255, 255, 0); // #FFFF00
            HighlightColorPreview.Background = new SolidColorBrush(HighlightColor);
            _isInitialized = false;
            HighlightColorTextBox.Text = "#FFFF00";
            _isInitialized = true;
        }

        private void GridOpacitySlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (!_isInitialized) return;
            GridOpacity = (byte)GridOpacitySlider.Value;
            if (GridOpacityText != null)
                GridOpacityText.Text = GridOpacity.ToString();
        }

        private void HighlightOpacitySlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (!_isInitialized) return;
            HighlightOpacity = (byte)HighlightOpacitySlider.Value;
            if (HighlightOpacityText != null)
                HighlightOpacityText.Text = HighlightOpacity.ToString();
        }

        private void CompressionComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // Just track selection, will be applied on Save
        }

        private void ColorSpaceComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ColorSpaceComboBox.SelectedItem is ComboBoxItem item && 
                int.TryParse(item.Tag?.ToString(), out int space))
            {
                _selectedColorSpace = (PatternGenerator.ColorSpace)space;
            }
        }

        private bool TryParseHexColor(string? hex, out Color color)
        {
            color = Colors.Transparent;
            if (string.IsNullOrEmpty(hex)) return false;
            if (!hex.StartsWith("#")) hex = "#" + hex;
            try { color = (Color)ColorConverter.ConvertFromString(hex); return true; }
            catch { return false; }
        }

        #endregion

        #region Custom Colors

        private void AddCustomColor_Click(object sender, RoutedEventArgs e)
        {
            if (Owner is MainWindow mainWindow)
            {
                var generator = mainWindow.GetPatternGenerator();
                if (generator == null) return;
                
                // Open the new AddColorWindow
                var addColorWindow = new AddColorWindow(
                    () => generator.GetGroups(),
                    (groupId) => generator.GetNextColorNumber(groupId)
                ) { Owner = this };
                
                if (addColorWindow.ShowDialog() == true)
                {
                    var color = addColorWindow.SelectedColor;
                    
                    // If new group was created, add it first
                    if (!string.IsNullOrEmpty(addColorWindow.NewGroupId))
                    {
                        // The AddCustomColor method will create the group if it doesn't exist
                    }
                    
                    // Add the color
                    generator.AddCustomColor(
                        addColorWindow.ColorId, 
                        color.R, color.G, color.B,
                        addColorWindow.ColorName,
                        addColorWindow.GroupId);
                    
                    // Reload colors display
                    LoadColorsData();
                    PopulateColorGroups();
                    PopulateGroupFilter();
                    PopulateColors("all");
                    
                    // Notify main window to reload groups
                    mainWindow.ReloadColorGroups();
                    
                    MessageBox.Show(
                        Loc.T("settings.custom_color_added", addColorWindow.ColorId),
                        Loc.T("settings.success"),
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);
                }
            }
        }

        private void ImportColors_Click(object sender, RoutedEventArgs e)
        {
            if (Owner is not MainWindow mainWindow) return;

            var generator = mainWindow.GetPatternGenerator();
            if (generator == null) return;

            var window = new AddGroupFromJsonWindow(() => generator.GetGroups()) { Owner = this };

            if (window.ShowDialog() != true) return;

            try
            {
                var (imported, updated, skipped) = generator.ImportColorsFromJsonWithGroup(
                    window.JsonFilePath, window.GroupId, window.GroupName);

                if (imported > 0 || updated > 0)
                {
                    LoadColorsData();
                    PopulateColors("all");
                    LoadColorGroups();
                    mainWindow.ReloadColorGroups();

                    string message = Loc.T("settings.colors_imported_detail", imported, updated);
                    if (skipped > 0)
                        message += "\n" + Loc.T("settings.colors_skipped_duplicates", skipped);

                    MessageBox.Show(message, Loc.T("settings.success"),
                        MessageBoxButton.OK, MessageBoxImage.Information);
                }
                else
                {
                    MessageBox.Show(Loc.T("settings.no_colors_imported"), Loc.T("settings.warning"),
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(Loc.T("settings.import_error", ex.Message), Loc.T("settings.error"),
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void DeleteColors_Click(object sender, RoutedEventArgs e)
        {
            if (Owner is not MainWindow mainWindow) return;
            var generator = mainWindow.GetPatternGenerator();
            if (generator == null) return;

            var allColors = generator.GetGroups()
                .SelectMany(g => g.Colors.Select(c => new { c.ColorId, c.Name, GroupName = g.Name }))
                .OrderBy(c => c.ColorId)
                .ToList();

            if (allColors.Count == 0)
            {
                MessageBox.Show("No colors available to delete.", "Delete Color(s)", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var dialog = new Window
            {
                Title = "Delete Color(s)",
                Width = 420,
                Height = 460,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Owner = this,
                ResizeMode = ResizeMode.NoResize,
                Background = new SolidColorBrush(Color.FromRgb(30, 30, 30))
            };

            var root = new Grid { Margin = new Thickness(16) };
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            var searchBox = new TextBox
            {
                Background = new SolidColorBrush(Color.FromRgb(62, 62, 66)),
                Foreground = Brushes.White,
                BorderBrush = new SolidColorBrush(Color.FromRgb(85, 85, 85)),
                Padding = new Thickness(8, 6, 8, 6),
                Margin = new Thickness(0, 0, 0, 8)
            };
            Grid.SetRow(searchBox, 0);

            var hint = new TextBlock
            {
                Text = "Hold Ctrl or Shift to select multiple colors.",
                Foreground = new SolidColorBrush(Color.FromRgb(136, 136, 136)),
                FontSize = 10,
                Margin = new Thickness(0, 0, 0, 6)
            };
            Grid.SetRow(hint, 1);

            var listBox = new ListBox
            {
                SelectionMode = SelectionMode.Extended,
                Background = new SolidColorBrush(Color.FromRgb(37, 37, 38)),
                Foreground = Brushes.White,
                BorderBrush = new SolidColorBrush(Color.FromRgb(85, 85, 85)),
                Margin = new Thickness(0, 0, 0, 12)
            };
            Grid.SetRow(listBox, 2);

            void RefreshList(string filter)
            {
                listBox.Items.Clear();
                foreach (var c in allColors)
                {
                    if (!string.IsNullOrEmpty(filter) &&
                        !c.ColorId.Contains(filter, StringComparison.OrdinalIgnoreCase) &&
                        !c.Name.Contains(filter, StringComparison.OrdinalIgnoreCase))
                        continue;
                    listBox.Items.Add(new ListBoxItem
                    {
                        Content = $"{c.ColorId}  —  {c.Name}  ({c.GroupName})",
                        Tag = c.ColorId,
                        Foreground = Brushes.White,
                        Background = Brushes.Transparent
                    });
                }
            }

            RefreshList("");
            searchBox.TextChanged += (s, ev) => RefreshList(searchBox.Text.Trim());

            var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
            Grid.SetRow(buttons, 3);

            var cancelBtn = new Button
            {
                Content = "Cancel", Width = 80, Padding = new Thickness(0, 6, 0, 6),
                Background = new SolidColorBrush(Color.FromRgb(62, 62, 66)),
                Foreground = Brushes.White, BorderThickness = new Thickness(0), Margin = new Thickness(0, 0, 8, 0)
            };
            var deleteBtn = new Button
            {
                Content = "Delete", Width = 80, Padding = new Thickness(0, 6, 0, 6),
                Background = new SolidColorBrush(Color.FromRgb(183, 0, 116)),
                Foreground = Brushes.White, BorderThickness = new Thickness(0)
            };

            cancelBtn.Click += (s, ev) => dialog.DialogResult = false;
            deleteBtn.Click += (s, ev) => dialog.DialogResult = true;

            buttons.Children.Add(cancelBtn);
            buttons.Children.Add(deleteBtn);

            root.Children.Add(searchBox);
            root.Children.Add(hint);
            root.Children.Add(listBox);
            root.Children.Add(buttons);
            dialog.Content = root;

            if (dialog.ShowDialog() != true) return;

            var selectedIds = listBox.SelectedItems
                .OfType<ListBoxItem>()
                .Select(item => item.Tag as string)
                .Where(id => id != null)
                .ToList();

            if (selectedIds.Count == 0) return;

            var confirm = MessageBox.Show(
                $"Delete {selectedIds.Count} color(s)? This cannot be undone.",
                "Confirm Delete",
                MessageBoxButton.OKCancel,
                MessageBoxImage.Warning);

            if (confirm != MessageBoxResult.OK) return;

            foreach (var id in selectedIds)
                generator.DeleteColor(id!);

            LoadColorsData();
            PopulateColorGroups();
            PopulateGroupFilter();
            PopulateColors("all");
            mainWindow.ReloadColorGroups();
        }

        private void DeleteGroup_Click(object sender, RoutedEventArgs e)
        {
            if (Owner is not MainWindow mainWindow) return;
            var generator = mainWindow.GetPatternGenerator();
            if (generator == null) return;

            if (_groups.Count == 0)
            {
                MessageBox.Show("No groups available to delete.", "Delete Group", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var dialog = new Window
            {
                Title = "Delete Group",
                Width = 360,
                Height = 175,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Owner = this,
                ResizeMode = ResizeMode.NoResize,
                Background = new SolidColorBrush(Color.FromRgb(30, 30, 30))
            };

            var stack = new StackPanel { Margin = new Thickness(16) };

            var groupLabel = new TextBlock { Text = "Color Group:", Foreground = Brushes.White, Margin = new Thickness(0, 0, 0, 4) };
            var groupComboBox = new ComboBox
            {
                Style = (Style)Resources["DarkComboBox"],
                Margin = new Thickness(0, 0, 0, 16)
            };
            foreach (var g in _groups)
                groupComboBox.Items.Add(new ComboBoxItem { Content = g.Name, Tag = g.GroupId });
            if (groupComboBox.Items.Count > 0)
                groupComboBox.SelectedIndex = 0;

            var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
            var cancelBtn = new Button
            {
                Content = "Cancel", Width = 80, Padding = new Thickness(0, 6, 0, 6),
                Background = new SolidColorBrush(Color.FromRgb(62, 62, 66)),
                Foreground = Brushes.White, BorderThickness = new Thickness(0), Margin = new Thickness(0, 0, 8, 0)
            };
            var deleteBtn = new Button
            {
                Content = "Delete", Width = 80, Padding = new Thickness(0, 6, 0, 6),
                Background = new SolidColorBrush(Color.FromRgb(183, 0, 116)),
                Foreground = Brushes.White, BorderThickness = new Thickness(0)
            };

            cancelBtn.Click += (s, ev) => dialog.DialogResult = false;
            deleteBtn.Click += (s, ev) => dialog.DialogResult = true;

            buttons.Children.Add(cancelBtn);
            buttons.Children.Add(deleteBtn);
            stack.Children.Add(groupLabel);
            stack.Children.Add(groupComboBox);
            stack.Children.Add(buttons);
            dialog.Content = stack;

            if (dialog.ShowDialog() != true) return;

            var selectedGroup = (groupComboBox.SelectedItem as ComboBoxItem)?.Tag as string ?? "";

            var confirm = MessageBox.Show(
                $"Delete the entire group '{(groupComboBox.SelectedItem as ComboBoxItem)?.Content}'? This will remove all colors in it and cannot be undone.",
                "Confirm Delete Group",
                MessageBoxButton.OKCancel,
                MessageBoxImage.Warning);

            if (confirm != MessageBoxResult.OK) return;

            generator.DeleteGroup(selectedGroup);

            LoadColorsData();
            PopulateColorGroups();
            PopulateGroupFilter();
            PopulateColors("all");
            mainWindow.ReloadColorGroups();
        }

        private string? ShowInputDialog(string title, string prompt, string defaultValue = "")
        {
            var dialog = new Window
            {
                Title = title,
                Width = 350,
                Height = 150,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Owner = this,
                ResizeMode = ResizeMode.NoResize,
                Background = new SolidColorBrush(Color.FromRgb(30, 30, 30))
            };

            var stack = new StackPanel { Margin = new Thickness(16) };
            var label = new TextBlock 
            { 
                Text = prompt, 
                Foreground = Brushes.White, 
                Margin = new Thickness(0, 0, 0, 8) 
            };
            var textBox = new TextBox 
            { 
                Text = defaultValue, 
                Background = new SolidColorBrush(Color.FromRgb(62, 62, 66)),
                Foreground = Brushes.White,
                BorderBrush = new SolidColorBrush(Color.FromRgb(85, 85, 85)),
                Padding = new Thickness(8, 6, 8, 6)
            };
            var buttons = new StackPanel 
            { 
                Orientation = Orientation.Horizontal, 
                HorizontalAlignment = HorizontalAlignment.Right, 
                Margin = new Thickness(0, 16, 0, 0) 
            };
            var okBtn = new Button 
            { 
                Content = "OK", 
                Width = 80, 
                Padding = new Thickness(0, 6, 0, 6),
                Background = new SolidColorBrush(Color.FromRgb(183, 0, 116)),
                Foreground = Brushes.White,
                BorderThickness = new Thickness(0),
                Margin = new Thickness(8, 0, 0, 0)
            };
            var cancelBtn = new Button 
            { 
                Content = "Cancel", 
                Width = 80, 
                Padding = new Thickness(0, 6, 0, 6),
                Background = new SolidColorBrush(Color.FromRgb(62, 62, 66)),
                Foreground = Brushes.White,
                BorderThickness = new Thickness(0)
            };

            string? result = null;
            okBtn.Click += (s, e) => { result = textBox.Text; dialog.DialogResult = true; };
            cancelBtn.Click += (s, e) => { dialog.DialogResult = false; };

            buttons.Children.Add(cancelBtn);
            buttons.Children.Add(okBtn);
            stack.Children.Add(label);
            stack.Children.Add(textBox);
            stack.Children.Add(buttons);
            dialog.Content = stack;

            return dialog.ShowDialog() == true ? result : null;
        }

        #endregion

        #region Save/Cancel

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            if (Owner is MainWindow mainWindow)
            {
                mainWindow.SetCanvasColor(CanvasColor);
                mainWindow.SetGridColor(Color.FromArgb(GridOpacity, GridColor.R, GridColor.G, GridColor.B));
                mainWindow.SetHighlightColor(Color.FromArgb(HighlightOpacity, HighlightColor.R, HighlightColor.G, HighlightColor.B));
                mainWindow.SetAccentColor(AccentColor);
                mainWindow.SetLanguage(_selectedLanguage);
                mainWindow.SetColorSpace(_selectedColorSpace);
                
                if (CompressionComboBox.SelectedItem is ComboBoxItem item && 
                    int.TryParse(item.Tag?.ToString(), out int level))
                {
                    mainWindow.SetCompressionLevel((PatternGenerator.CompressionLevel)level);
                }
                
                mainWindow.SaveSettings();
            }
            
            SaveColorsData();
            DialogResult = true;
            Close();
        }

        #endregion
    }
}
