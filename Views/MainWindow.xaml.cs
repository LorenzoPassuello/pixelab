// ============================================================================
// MainWindow.xaml.cs
// Main application window for PixelArt Viewer
// Handles image display, pattern generation, and user interactions
// ============================================================================

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Windows.Threading;
using Microsoft.Win32;
using IO = System.IO;

namespace Pixelab
{
    /// <summary>
    /// Main application window that provides pixel art viewing and bead pattern generation functionality.
    /// Features include: image loading, zoom/pan, grid overlay, pattern generation, and color highlighting.
    /// </summary>
    public partial class MainWindow : Window
    {
        // ============================================================================
        // CONSTANTS
        // ============================================================================
        
        /// <summary>Minimum allowed zoom level (10%)</summary>
        private const double MinZoom = 0.1;
        
        /// <summary>Maximum allowed zoom level (10000%)</summary>
        private const double MaxZoom = 100.0;
        
        /// <summary>Multiplier for zoom in/out operations</summary>
        private const double ZoomFactor = 1.2;

        // ============================================================================
        // VIEWPORT STATE
        // ============================================================================
        
        /// <summary>Current zoom level (1.0 = 100%)</summary>
        private double _zoom = 1.0;
        
        /// <summary>Whether the user is currently panning the viewport</summary>
        private bool _isPanning = false;
        
        /// <summary>Last mouse position during pan operation</summary>
        private Point _lastMousePosition;

        // ============================================================================
        // IMAGE DATA
        // ============================================================================
        
        /// <summary>Currently loaded image bitmap</summary>
        private BitmapSource? _currentImage;
        
        /// <summary>File path of the currently loaded image</summary>
        private string? _currentImagePath;
        
        /// <summary>Width of the current image in pixels</summary>
        private int _imageWidth = 0;
        
        /// <summary>Height of the current image in pixels</summary>
        private int _imageHeight = 0;
        
        /// <summary>Raw pixel data (BGRA format) for color picking</summary>
        private byte[]? _currentPixels;
        
        /// <summary>Stride (bytes per row) of the pixel data</summary>
        private int _currentStride;

        // ============================================================================
        // LAYER VISIBILITY STATE
        // ============================================================================
        
        /// <summary>Whether the pixel grid overlay is currently visible</summary>
        private bool _isGridVisible = false;
        
        /// <summary>Whether a generated pattern image exists</summary>
        private bool _hasGeneratedImage = false;
        
        /// <summary>Current pattern data (colors and pixel positions)</summary>
        private PatternGenerator.PatternData? _currentPattern;

        // ============================================================================
        // APPEARANCE SETTINGS
        // ============================================================================
        
        /// <summary>Background color of the canvas area</summary>
        private Color _canvasColor = Color.FromRgb(255, 255, 255);
        
        /// <summary>Color of the pixel grid lines (with alpha)</summary>
        private Color _gridColor = Color.FromArgb(150, 128, 128, 128);
        
        /// <summary>Color used for highlighting selected bead colors</summary>
        private Color _highlightColor = Color.FromArgb(200, 255, 255, 0);
        
        /// <summary>Accent color for UI elements (buttons, highlights)</summary>
        private Color _accentColor = Color.FromRgb(183, 0, 116); // #B70074
        
        /// <summary>Last opened directory for file dialogs</summary>
        private string _lastOpenedDirectory = "";
        
        /// <summary>Current compression level for pattern generation</summary>
        private PatternGenerator.CompressionLevel _compressionLevel = PatternGenerator.CompressionLevel.Off;
        
        /// <summary>Current color space for pattern generation (RGB or Lab)</summary>
        private PatternGenerator.ColorSpace _colorSpace = PatternGenerator.ColorSpace.Lab;
        
        /// <summary>Selected group IDs for pattern generation (empty = all groups)</summary>
        private HashSet<string> _selectedGroupIds = new();

        // ============================================================================
        // HIGHLIGHT ANIMATION STATE
        // ============================================================================
        
        /// <summary>Currently highlighted color ID (null if none)</summary>
        private string? _highlightedColorId = null;
        
        /// <summary>Timer for highlight blinking animation</summary>
        private DispatcherTimer? _highlightTimer;
        
        /// <summary>Current visibility state of the highlight (for blinking)</summary>
        private bool _highlightVisible = true;

        // ============================================================================
        // FILE HANDLING
        // ============================================================================
        
        /// <summary>Supported image file extensions</summary>
        private readonly string[] _supportedExtensions = { ".png", ".jpg", ".jpeg", ".bmp", ".gif" };
        
        /// <summary>Path to the application settings file</summary>
        private readonly string _settingsPath;
        
        /// <summary>Path to the bead colors database file</summary>
        private readonly string _colorsPath;
        
        /// <summary>Pattern generator instance for creating bead patterns</summary>
        private PatternGenerator? _patternGenerator;
        
        /// <summary>Current application language code</summary>
        private string _currentLanguage = "en";

        // ============================================================================
        // UI ELEMENT REFERENCES
        // ============================================================================
        
        /// <summary>Maps RGB color keys to their border elements in the original palette</summary>
        private Dictionary<int, Border> _originalColorBorders = new();
        
        /// <summary>Maps bead color IDs to their border elements in the beads palette</summary>
        private Dictionary<string, Border> _beadsColorBorders = new();

        /// <summary>Shortcut reference to the localization manager instance</summary>
        private static LocalizationManager Loc => LocalizationManager.Instance;

        // ============================================================================
        // CONSTRUCTOR
        // ============================================================================
        
        /// <summary>
        /// Initializes the main window and all required components.
        /// </summary>
        public MainWindow()
        {
            InitializeComponent();
            
            // Register event handlers
            KeyDown += MainWindow_KeyDown;
            Closing += MainWindow_Closing;
            
            // Initialize AppData folder for user data
            string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            string pixelabFolder = IO.Path.Combine(appData, "Pixelab");
            
            // Ensure Pixelab folder exists in AppData
            if (!IO.Directory.Exists(pixelabFolder))
            {
                IO.Directory.CreateDirectory(pixelabFolder);
            }
            
            // Set paths in AppData
            _settingsPath = IO.Path.Combine(pixelabFolder, "settings.json");
            _colorsPath = IO.Path.Combine(pixelabFolder, "colors.json");

            // Find default resource files (shipped with the application)
            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            string defaultColorsPath = IO.Path.Combine(baseDir, "Resources", "Colors", "colors.json");

            // Fallback to relative paths for development environment
            if (!IO.File.Exists(defaultColorsPath))
                defaultColorsPath = IO.Path.Combine("Resources", "Colors", "colors.json");

            // Copy default colors to AppData if they don't exist
            if (!IO.File.Exists(_colorsPath) && IO.File.Exists(defaultColorsPath))
                IO.File.Copy(defaultColorsPath, _colorsPath);

            // Initialize the pattern generator
            _patternGenerator = new PatternGenerator(_colorsPath);
            
            // Setup highlight animation timer (blinks every 400ms)
            _highlightTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(400) };
            _highlightTimer.Tick += HighlightTimer_Tick;
            
            // Load user settings and apply localization
            LoadSettings();
            ApplyLocalization();
        }

        // ============================================================================
        // LOCALIZATION
        // ============================================================================
        
        /// <summary>
        /// Applies localized strings to all UI elements.
        /// Called after loading settings and when language changes.
        /// </summary>
        private void ApplyLocalization()
        {
            // Menu buttons
            FileMenuButton.Content = Loc.T("menu.file");
            ViewMenuButton.Content = Loc.T("menu.view");
            
            // File menu items
            OpenFileButton.Content = $"📁  {Loc.T("menu.open_file")}";
            OpenFolderButton.Content = $"📂  {Loc.T("menu.open_folder")}";
            
            // View menu items
            ZoomInButton.Content = $"🔍  {Loc.T("menu.zoom_in")}";
            ZoomOutButton.Content = $"🔎  {Loc.T("menu.zoom_out")}";
            ResetViewButton.Content = $"🎯  {Loc.T("menu.reset_view")}";
            FitToWindowButton.Content = $"📐  {Loc.T("menu.fit_to_window")}";
            ActualSizeButton.Content = $"📏  {Loc.T("menu.actual_size")}";
            
            // Grid toggle button (shows current state)
            UpdateGridButtonText();
            
            // Section headers
            ImageInfoHeader.Text = Loc.T("panels.image_info");
            BeadsInfoHeader.Text = Loc.T("panels.beads_info");
            FileInfoHeader.Text = Loc.T("panels.file_info");
            
            // Image info labels
            FileNameLabel.Text = Loc.T("labels.file_name");
            DimensionsLabel.Text = Loc.T("labels.dimensions");
            UniqueColorsLabel.Text = Loc.T("labels.unique_colors");
            ColorPaletteLabel.Text = Loc.T("labels.color_palette_original");
            
            // Beads info labels
            EstimatedBeadsLabel.Text = Loc.T("labels.estimated_beads");
            AlphaThresholdLabel.Text = Loc.T("labels.alpha_threshold");
            AlphaThresholdDesc.Text = Loc.T("labels.alpha_threshold_desc");
            BeadsPaletteLabel.Text = Loc.T("labels.beads_palette");
            BeadsPaletteHint.Text = Loc.T("labels.beads_palette_hint");
            
            // File info labels
            FileNameLabel2.Text = Loc.T("labels.file_name");
            FilePathLabel.Text = Loc.T("labels.file_path");
            DimensionsLabel2.Text = Loc.T("labels.dimensions");
            TotalPixelsLabel.Text = Loc.T("labels.total_pixels");
            FileSizeLabel.Text = Loc.T("labels.file_size");
            
            // Buttons
            GeneratePatternButton.Content = Loc.T("buttons.generate_pattern");
            ShowGeneratedImageCheckbox.Content = Loc.T("buttons.show_generated_image");
            
            // Color group label
            ColorGroupLabel.Text = Loc.T("labels.color_group");
            
            // Placeholder text
            PlaceholderText.Text = Loc.T("messages.drop_image");
            
            // Load color groups into ComboBox
            LoadColorGroups();
        }
        
        /// <summary>
        /// Loads available color groups into the ColorGroupComboBox as checkboxes.
        /// </summary>
        private void LoadColorGroups()
        {
            if (_patternGenerator == null) return;

            var groups = _patternGenerator.GetGroups().Where(g => g.Enabled).ToList();
            ColorGroupComboBox.Items.Clear();

            // Header item — always selected, shows selection summary
            _groupHeaderItem = new ComboBoxItem
            {
                Content = Loc.T("labels.all_groups"),
                IsHitTestVisible = false,
                Focusable = false
            };
            ColorGroupComboBox.Items.Add(_groupHeaderItem);

            // Add each enabled group as a CheckBox item
            foreach (var group in groups)
            {
                var cb = new CheckBox
                {
                    Content = group.Name,
                    Tag = group.GroupId,
                    IsChecked = _selectedGroupIds.Count == 0 || _selectedGroupIds.Contains(group.GroupId),
                    Foreground = System.Windows.Media.Brushes.White,
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(2, 2, 2, 2)
                };
                cb.Click += GroupCheckBox_Click;

                ColorGroupComboBox.Items.Add(new ComboBoxItem { Content = cb, Padding = new Thickness(4, 2, 4, 2) });
            }

            ColorGroupComboBox.SelectedIndex = 0;
            UpdateGroupFilterHeader();
        }

        private ComboBoxItem? _groupHeaderItem;

        private void GroupCheckBox_Click(object sender, RoutedEventArgs e)
        {
            // Prevent the ComboBoxItem from processing this click (which would close the dropdown)
            e.Handled = true;

            // Recompute _selectedGroupIds from current checkbox states
            _selectedGroupIds.Clear();
            foreach (ComboBoxItem item in ColorGroupComboBox.Items.Cast<ComboBoxItem>().Skip(1))
            {
                if (item.Content is CheckBox cb && cb.IsChecked == true)
                    _selectedGroupIds.Add((string)cb.Tag!);
            }

            UpdateGroupFilterHeader();
            ColorGroupComboBox.IsDropDownOpen = true;
        }

        private void UpdateGroupFilterHeader()
        {
            if (_groupHeaderItem == null) return;

            int total = ColorGroupComboBox.Items.Count - 1;
            int checked_ = _selectedGroupIds.Count;

            if (checked_ == 0 || checked_ == total)
            {
                _selectedGroupIds.Clear();
                _groupHeaderItem.Content = Loc.T("labels.all_groups");
                // Sync checkboxes to all-checked
                foreach (ComboBoxItem item in ColorGroupComboBox.Items.Cast<ComboBoxItem>().Skip(1))
                    if (item.Content is CheckBox cb) cb.IsChecked = true;
            }
            else
            {
                var names = ColorGroupComboBox.Items.Cast<object>().Skip(1)
                    .OfType<ComboBoxItem>()
                    .Where(i => i.Content is CheckBox cb && cb.IsChecked == true)
                    .Select(i => ((CheckBox)i.Content).Content?.ToString() ?? "")
                    .ToList();
                _groupHeaderItem.Content = string.Join(", ", names);
            }
        }

        /// <summary>
        /// Updates the grid toggle button text based on current grid visibility state.
        /// </summary>
        private void UpdateGridButtonText()
        {
            if (_isGridVisible)
                ShowGridButton.Content = $"✓  {Loc.T("menu.hide_grid")}";
            else
                ShowGridButton.Content = $"🔘  {Loc.T("menu.show_grid")}";
        }

        // ============================================================================
        // SETTINGS MANAGEMENT
        // ============================================================================
        #region Settings

        /// <summary>
        /// Loads application settings from the settings file.
        /// Includes canvas colors, grid settings, language preference, etc.
        /// </summary>
        private void LoadSettings()
        {
            try
            {
                if (IO.File.Exists(_settingsPath))
                {
                    string json = IO.File.ReadAllText(_settingsPath);
                    var settings = JsonSerializer.Deserialize<AppSettings>(json);
                    
                    if (settings != null)
                    {
                        // Load canvas background color
                        if (TryParseHexColor(settings.Canvas?.BackgroundColor, out Color canvasColor))
                            _canvasColor = canvasColor;
                        
                        // Load grid color and opacity
                        if (TryParseHexColor(settings.Grid?.Color, out Color gridColor))
                        {
                            byte opacity = (byte)(settings.Grid?.Opacity ?? 150);
                            _gridColor = Color.FromArgb(opacity, gridColor.R, gridColor.G, gridColor.B);
                        }
                        
                        // Load highlight color and opacity
                        if (TryParseHexColor(settings.Highlight?.Color, out Color highlightColor))
                        {
                            byte opacity = (byte)(settings.Highlight?.Opacity ?? 200);
                            _highlightColor = Color.FromArgb(opacity, highlightColor.R, highlightColor.G, highlightColor.B);
                        }
                        
                        // Load accent color
                        if (TryParseHexColor(settings.Appearance?.AccentColor, out Color accentColor))
                            _accentColor = accentColor;
                        
                        // Load other settings
                        _isGridVisible = settings.Grid?.Visible ?? false;
                        _lastOpenedDirectory = settings.Files?.LastOpenedDirectory ?? "";
                        _currentLanguage = settings.Language ?? "en";
                        
                        // Load beads settings
                        if (settings.Beads != null)
                        {
                            AlphaThresholdSlider.Value = settings.Beads.AlphaThreshold;
                            _compressionLevel = (PatternGenerator.CompressionLevel)(settings.Beads.ColorCompression);
                            _colorSpace = (PatternGenerator.ColorSpace)(settings.Beads.ColorSpace);
                        }
                    }
                }
            }
            catch
            {
                // Use default settings if loading fails
            }
            
            // Load the selected language
            Loc.LoadLanguage(_currentLanguage);
            
            // Apply visual settings
            ApplySettings();
            
            // Auto-load last opened directory if it exists
            if (!string.IsNullOrEmpty(_lastOpenedDirectory) && IO.Directory.Exists(_lastOpenedDirectory))
                Dispatcher.BeginInvoke(new Action(() => LoadFolder(_lastOpenedDirectory)), DispatcherPriority.Loaded);
        }

        /// <summary>
        /// Saves current application settings to the settings file.
        /// </summary>
        public void SaveSettings()
        {
            try
            {
                var settings = new AppSettings
                {
                    Canvas = new CanvasSettings 
                    { 
                        BackgroundColor = $"#{_canvasColor.R:X2}{_canvasColor.G:X2}{_canvasColor.B:X2}" 
                    },
                    Grid = new GridSettings 
                    { 
                        Color = $"#{_gridColor.R:X2}{_gridColor.G:X2}{_gridColor.B:X2}", 
                        Opacity = _gridColor.A, 
                        Visible = _isGridVisible 
                    },
                    Highlight = new HighlightSettings 
                    { 
                        Color = $"#{_highlightColor.R:X2}{_highlightColor.G:X2}{_highlightColor.B:X2}", 
                        Opacity = _highlightColor.A 
                    },
                    Appearance = new AppearanceSettings
                    {
                        AccentColor = $"#{_accentColor.R:X2}{_accentColor.G:X2}{_accentColor.B:X2}"
                    },
                    View = new ViewSettings { LastZoom = _zoom },
                    Files = new FilesSettings 
                    { 
                        LastOpenedDirectory = _lastOpenedDirectory, 
                        RecentFiles = new List<string>() 
                    },
                    Window = new WindowSettings 
                    { 
                        Width = (int)Width, 
                        Height = (int)Height, 
                        Maximized = WindowState == WindowState.Maximized 
                    },
                    Beads = new BeadsSettings 
                    { 
                        AlphaThreshold = (int)AlphaThresholdSlider.Value, 
                        ColorCompression = (int)_compressionLevel,
                        ColorSpace = (int)_colorSpace
                    },
                    Language = _currentLanguage
                };
                
                // Ensure settings directory exists
                string? dir = IO.Path.GetDirectoryName(_settingsPath);
                if (!string.IsNullOrEmpty(dir) && !IO.Directory.Exists(dir))
                    IO.Directory.CreateDirectory(dir);
                
                // Write settings file
                IO.File.WriteAllText(_settingsPath, JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true }));
            }
            catch
            {
                // Silently ignore save errors
            }
        }

        /// <summary>
        /// Handles window closing event - saves settings before exit.
        /// </summary>
        private void MainWindow_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
        {
            SaveSettings();
        }

        /// <summary>
        /// Returns current appearance settings for the Settings window.
        /// </summary>
        public (Color canvasColor, Color gridColor, Color highlightColor, Color accentColor, PatternGenerator.CompressionLevel compression, PatternGenerator.ColorSpace colorSpace) GetCurrentSettings()
        {
            return (_canvasColor, _gridColor, _highlightColor, _accentColor, _compressionLevel, _colorSpace);
        }
        
        /// <summary>Returns the current compression level setting.</summary>
        public PatternGenerator.CompressionLevel GetCompressionLevel() => _compressionLevel;
        
        /// <summary>Returns the current color space setting.</summary>
        public PatternGenerator.ColorSpace GetColorSpace() => _colorSpace;
        
        /// <summary>Returns the current accent color.</summary>
        public Color GetAccentColor() => _accentColor;
        
        /// <summary>Returns the current language code.</summary>
        public string GetCurrentLanguage() => _currentLanguage;
        
        /// <summary>Sets the language code (applied on next restart).</summary>
        public void SetLanguage(string code) => _currentLanguage = code;

        /// <summary>
        /// Applies visual settings to UI elements.
        /// </summary>
        private void ApplySettings()
        {
            CanvasBackground.Background = new SolidColorBrush(_canvasColor);
            ApplyAccentColor(_accentColor);
            UpdateGridButtonText();
        }
        
        /// <summary>
        /// Applies the accent color to all UI elements by replacing resource brushes.
        /// </summary>
        private void ApplyAccentColor(Color accent)
        {
            // Calculate dark and light variants
            Color accentDark = Color.FromRgb(
                (byte)Math.Max(0, accent.R - 25),
                (byte)Math.Max(0, accent.G - 25),
                (byte)Math.Max(0, accent.B - 25));
            Color accentLight = Color.FromRgb(
                (byte)Math.Min(255, (int)(accent.R * 0.6 + 255 * 0.4)),
                (byte)Math.Min(255, (int)(accent.G * 0.6 + 255 * 0.4)),
                (byte)Math.Min(255, (int)(accent.B * 0.6 + 255 * 0.4)));
            
            // Replace the brush resources with new ones (XAML brushes are frozen/read-only)
            Application.Current.Resources["TopBarBrush"] = new SolidColorBrush(accentDark);
            Application.Current.Resources["LeftPanelBrush"] = new SolidColorBrush(accent);
            Application.Current.Resources["RightPanelBrush"] = new SolidColorBrush(accent);
            Application.Current.Resources["ButtonBrush"] = new SolidColorBrush(accent);
            Application.Current.Resources["ButtonHoverBrush"] = new SolidColorBrush(accentDark);
            Application.Current.Resources["TextBoxBrush"] = new SolidColorBrush(accentLight);
            Application.Current.Resources["TextBoxBorderBrush"] = new SolidColorBrush(accentDark);
        }

        /// <summary>Sets the canvas background color.</summary>
        public void SetCanvasColor(Color color)
        {
            _canvasColor = color;
            CanvasBackground.Background = new SolidColorBrush(_canvasColor);
        }

        /// <summary>Sets the grid line color and rebuilds the grid if visible.</summary>
        public void SetGridColor(Color color)
        {
            _gridColor = color;
            if (_isGridVisible && _currentImage != null)
                BuildGridLayer();
        }

        /// <summary>Sets the highlight color and rebuilds highlights if active.</summary>
        public void SetHighlightColor(Color color)
        {
            _highlightColor = color;
            if (_highlightedColorId != null)
                BuildHighlightLayer();
        }
        
        /// <summary>Sets the accent color for UI elements.</summary>
        public void SetAccentColor(Color color)
        {
            _accentColor = color;
            ApplyAccentColor(color);
        }

        /// <summary>Sets the compression level for pattern generation.</summary>
        public void SetCompressionLevel(PatternGenerator.CompressionLevel level) => _compressionLevel = level;
        
        /// <summary>Sets the color space for pattern generation.</summary>
        public void SetColorSpace(PatternGenerator.ColorSpace space) => _colorSpace = space;
        
        /// <summary>Gets the pattern generator instance.</summary>
        public PatternGenerator? GetPatternGenerator() => _patternGenerator;
        
        /// <summary>Gets the colors file path (in AppData).</summary>
        public string GetColorsPath() => _colorsPath;
        
        /// <summary>Reloads the color groups ComboBox after colors are added/imported.</summary>
        public void ReloadColorGroups()
        {
            _patternGenerator?.ReloadColors();
            LoadColorGroups();
        }

        /// <summary>
        /// Attempts to parse a hex color string (with or without #) into a Color.
        /// </summary>
        private bool TryParseHexColor(string? hex, out Color color)
        {
            color = Colors.Transparent;
            if (string.IsNullOrEmpty(hex)) return false;
            if (!hex.StartsWith("#")) hex = "#" + hex;
            try
            {
                color = (Color)ColorConverter.ConvertFromString(hex);
                return true;
            }
            catch
            {
                return false;
            }
        }

        #endregion

        // ============================================================================
        // MENU HANDLERS
        // ============================================================================
        #region Menu

        /// <summary>Toggles the File menu popup visibility.</summary>
        private void FileMenuButton_Click(object sender, RoutedEventArgs e)
        {
            FileMenuPopup.IsOpen = !FileMenuPopup.IsOpen;
            ViewMenuPopup.IsOpen = false;
        }

        /// <summary>Toggles the View menu popup visibility.</summary>
        private void ViewMenuButton_Click(object sender, RoutedEventArgs e)
        {
            ViewMenuPopup.IsOpen = !ViewMenuPopup.IsOpen;
            FileMenuPopup.IsOpen = false;
        }

        /// <summary>Opens the Settings window.</summary>
        private void SettingsButton_Click(object sender, RoutedEventArgs e)
        {
            new SettingsWindow { Owner = this }.ShowDialog();
        }

        /// <summary>Opens a file dialog to select and load a single image.</summary>
        private void OpenFile_Click(object sender, RoutedEventArgs e)
        {
            FileMenuPopup.IsOpen = false;
            
            var dialog = new OpenFileDialog
            {
                Title = Loc.T("menu.open_file"),
                Filter = "Image Files|*.png;*.jpg;*.jpeg;*.bmp;*.gif|All Files|*.*",
                InitialDirectory = _lastOpenedDirectory
            };
            
            if (dialog.ShowDialog() == true)
            {
                _lastOpenedDirectory = IO.Path.GetDirectoryName(dialog.FileName) ?? "";
                LoadImage(dialog.FileName);
            }
        }

        /// <summary>Opens a folder browser dialog to load a folder of images.</summary>
        private void OpenFolder_Click(object sender, RoutedEventArgs e)
        {
            FileMenuPopup.IsOpen = false;
            
            var dialog = new System.Windows.Forms.FolderBrowserDialog
            {
                SelectedPath = _lastOpenedDirectory
            };
            
            if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                _lastOpenedDirectory = dialog.SelectedPath;
                LoadFolder(dialog.SelectedPath);
            }
        }

        /// <summary>Zooms in by the zoom factor.</summary>
        private void ZoomIn_Click(object sender, RoutedEventArgs e)
        {
            ViewMenuPopup.IsOpen = false;
            ZoomAtCenter(_zoom * ZoomFactor);
        }

        /// <summary>Zooms out by the zoom factor.</summary>
        private void ZoomOut_Click(object sender, RoutedEventArgs e)
        {
            ViewMenuPopup.IsOpen = false;
            ZoomAtCenter(_zoom / ZoomFactor);
        }

        /// <summary>Resets zoom to 100% and centers the image.</summary>
        private void ResetView_Click(object sender, RoutedEventArgs e)
        {
            ViewMenuPopup.IsOpen = false;
            if (_currentImage == null) return;
            _zoom = 1.0;
            CenterLayers();
            UpdateZoomDisplay();
        }

        /// <summary>Fits the image to fill the viewport while maintaining aspect ratio.</summary>
        private void FitToWindow_Click(object sender, RoutedEventArgs e)
        {
            ViewMenuPopup.IsOpen = false;
            FitToWindow();
        }

        /// <summary>Sets zoom to exactly 100% (1:1 pixel mapping).</summary>
        private void ActualSize_Click(object sender, RoutedEventArgs e)
        {
            ViewMenuPopup.IsOpen = false;
            if (_currentImage == null) return;
            _zoom = 1.0;
            CenterLayers();
            UpdateZoomDisplay();
        }

        /// <summary>Toggles the pixel grid overlay visibility.</summary>
        private void ShowGrid_Click(object sender, RoutedEventArgs e)
        {
            ViewMenuPopup.IsOpen = false;
            if (_currentImage == null) return;
            
            _isGridVisible = !_isGridVisible;
            
            if (_isGridVisible)
            {
                BuildGridLayer();
                GridLayer.Visibility = Visibility.Visible;
            }
            else
            {
                GridLayer.Visibility = Visibility.Collapsed;
            }
            
            UpdateGridButtonText();
        }

        #endregion

        // ============================================================================
        // PATTERN GENERATION
        // ============================================================================
        #region Pattern Generation
        
        /// <summary>
        /// Handles color group selection changes.
        /// </summary>
        private void ColorGroupComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // Always keep the header item selected so the summary text stays visible
            if (ColorGroupComboBox.SelectedIndex != 0)
                ColorGroupComboBox.SelectedIndex = 0;
        }

        /// <summary>
        /// Handles the Generate Pattern button click.
        /// </summary>
        private void GeneratePattern_Click(object sender, RoutedEventArgs e)
        {
            if (_currentImage == null || _currentImagePath == null) return;

            try
            {
                GenerateNewPattern();
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    Loc.T("messages.error_loading_image", ex.Message),
                    Loc.T("messages.error_pattern"),
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// Generates a new bead pattern from the current image.
        /// </summary>
        private void GenerateNewPattern()
        {
            var (pattern, generatedImage) = _patternGenerator!.GeneratePattern(
                _currentImage!,
                _currentImagePath!,
                (int)AlphaThresholdSlider.Value,
                _compressionLevel,
                _colorSpace,
                _selectedGroupIds.Count > 0 ? _selectedGroupIds : null);
            
            _currentPattern = pattern;
            ApplyGeneratedImage(generatedImage, pattern.Colors.Count);
        }

        /// <summary>
        /// Applies a generated pattern image to the viewport and updates UI.
        /// </summary>
        private void ApplyGeneratedImage(BitmapSource image, int colorsUsed)
        {
            // Set up the generated image layer
            GeneratedImageLayer.Source = image;
            GeneratedImageLayer.Width = _imageWidth;
            GeneratedImageLayer.Height = _imageHeight;
            
            // Update state
            _hasGeneratedImage = true;
            ShowGeneratedImageCheckbox.IsEnabled = true;
            ShowGeneratedImageCheckbox.IsChecked = true;
            GeneratedImageLayer.Visibility = Visibility.Visible;
            
            // Update info text
            PatternColorsUsedText.Text = Loc.T("labels.pattern_uses_colors", colorsUsed);
            
            // Display the beads palette
            DisplayBeadsPalette();
        }

        /// <summary>
        /// Displays the beads palette panel with clickable color swatches.
        /// Each swatch can be clicked to highlight those pixels in the image.
        /// </summary>
        private void DisplayBeadsPalette()
        {
            BeadsPalettePanel.Children.Clear();
            _beadsColorBorders.Clear();
            
            if (_currentPattern == null) return;
            
            var colorLookup = _patternGenerator!.GetColorLookup();

            // Create color swatches sorted by pixel count (most used first)
            foreach (var patternColor in _currentPattern.Colors.OrderByDescending(c => c.Pixels.Count))
            {
                if (colorLookup.TryGetValue(patternColor.ColorId, out var color))
                {
                    var border = new Border
                    {
                        Width = 24,
                        Height = 24,
                        Margin = new Thickness(2),
                        Background = new SolidColorBrush(Color.FromRgb(color.R, color.G, color.B)),
                        BorderBrush = Brushes.White,
                        BorderThickness = new Thickness(2),
                        CornerRadius = new CornerRadius(3),
                        Tag = patternColor.ColorId,
                        Cursor = Cursors.Hand,
                        ToolTip = Loc.T("tooltips.color_beads", patternColor.ColorId, color.Name, patternColor.Pixels.Count)
                    };
                    
                    border.MouseLeftButtonDown += BeadColor_Click;
                    _beadsColorBorders[patternColor.ColorId] = border;
                    BeadsPalettePanel.Children.Add(border);
                }
            }
        }

        /// <summary>
        /// Handles click on a bead color swatch to toggle highlighting.
        /// </summary>
        private void BeadColor_Click(object sender, MouseButtonEventArgs e)
        {
            if (sender is Border border && border.Tag is string colorId)
            {
                if (_highlightedColorId == colorId)
                {
                    // Toggle off - same color clicked again
                    ClearHighlight();
                }
                else
                {
                    // Highlight new color
                    ClearHighlight();
                    _highlightedColorId = colorId;
                    border.BorderBrush = new SolidColorBrush(_highlightColor);
                    border.BorderThickness = new Thickness(3);
                    BuildHighlightLayer();
                    _highlightTimer?.Start();
                }
            }
        }

        /// <summary>
        /// Clears any active color highlighting.
        /// </summary>
        private void ClearHighlight()
        {
            _highlightTimer?.Stop();
            _highlightedColorId = null;
            HighlightLayer.Children.Clear();
            HighlightLayer.Visibility = Visibility.Collapsed;
            
            // Reset all bead color borders to default appearance
            foreach (var kvp in _beadsColorBorders)
            {
                kvp.Value.BorderBrush = Brushes.White;
                kvp.Value.BorderThickness = new Thickness(2);
            }
        }

        /// <summary>
        /// Builds the highlight layer with grid lines around pixels of the selected color.
        /// Only draws lines on outer edges (not between adjacent same-color pixels).
        /// </summary>
        private void BuildHighlightLayer()
        {
            HighlightLayer.Children.Clear();
            
            if (_currentPattern == null || _highlightedColorId == null) return;
            
            var patternColor = _currentPattern.Colors.FirstOrDefault(c => c.ColorId == _highlightedColorId);
            if (patternColor == null) return;
            
            var brush = new SolidColorBrush(_highlightColor);
            double thickness = 1.0 / _zoom; // Scale line thickness with zoom
            
            // Create a set of pixel positions for quick adjacency lookup
            var pixelSet = new HashSet<(int, int)>(patternColor.Pixels.Select(p => (p.X, p.Y)));
            
            // Draw grid lines around each pixel, but only on edges not shared with same-color pixels
            foreach (var px in patternColor.Pixels)
            {
                // Top edge
                if (!pixelSet.Contains((px.X, px.Y - 1)))
                {
                    var line = new Line
                    {
                        X1 = px.X, Y1 = px.Y,
                        X2 = px.X + 1, Y2 = px.Y,
                        Stroke = brush,
                        StrokeThickness = thickness,
                        SnapsToDevicePixels = true
                    };
                    line.SetValue(RenderOptions.EdgeModeProperty, EdgeMode.Aliased);
                    HighlightLayer.Children.Add(line);
                }
                
                // Bottom edge
                if (!pixelSet.Contains((px.X, px.Y + 1)))
                {
                    var line = new Line
                    {
                        X1 = px.X, Y1 = px.Y + 1,
                        X2 = px.X + 1, Y2 = px.Y + 1,
                        Stroke = brush,
                        StrokeThickness = thickness,
                        SnapsToDevicePixels = true
                    };
                    line.SetValue(RenderOptions.EdgeModeProperty, EdgeMode.Aliased);
                    HighlightLayer.Children.Add(line);
                }
                
                // Left edge
                if (!pixelSet.Contains((px.X - 1, px.Y)))
                {
                    var line = new Line
                    {
                        X1 = px.X, Y1 = px.Y,
                        X2 = px.X, Y2 = px.Y + 1,
                        Stroke = brush,
                        StrokeThickness = thickness,
                        SnapsToDevicePixels = true
                    };
                    line.SetValue(RenderOptions.EdgeModeProperty, EdgeMode.Aliased);
                    HighlightLayer.Children.Add(line);
                }
                
                // Right edge
                if (!pixelSet.Contains((px.X + 1, px.Y)))
                {
                    var line = new Line
                    {
                        X1 = px.X + 1, Y1 = px.Y,
                        X2 = px.X + 1, Y2 = px.Y + 1,
                        Stroke = brush,
                        StrokeThickness = thickness,
                        SnapsToDevicePixels = true
                    };
                    line.SetValue(RenderOptions.EdgeModeProperty, EdgeMode.Aliased);
                    HighlightLayer.Children.Add(line);
                }
            }
            
            HighlightLayer.Visibility = Visibility.Visible;
        }

        /// <summary>
        /// Timer tick handler for highlight blinking animation.
        /// </summary>
        private void HighlightTimer_Tick(object? sender, EventArgs e)
        {
            _highlightVisible = !_highlightVisible;
            HighlightLayer.Opacity = _highlightVisible ? 1.0 : 0.3;
        }

        /// <summary>
        /// Handles the "Show Generated Image" checkbox state change.
        /// </summary>
        private void ShowGeneratedImage_Changed(object sender, RoutedEventArgs e)
        {
            if (_hasGeneratedImage)
            {
                GeneratedImageLayer.Visibility = ShowGeneratedImageCheckbox.IsChecked == true
                    ? Visibility.Visible
                    : Visibility.Collapsed;
            }
        }

        /// <summary>
        /// Handles alpha threshold slider value changes.
        /// Updates the display text and recalculates bead count.
        /// </summary>
        private void AlphaThreshold_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (AlphaThresholdText != null)
                AlphaThresholdText.Text = ((int)AlphaThresholdSlider.Value).ToString();
            
            if (_currentImage != null)
                RecalculateBeads();
        }

        /// <summary>
        /// Handles slider click to immediately jump to clicked position.
        /// </summary>
        private void Slider_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (sender is Slider slider)
            {
                var pos = e.GetPosition(slider);
                double ratio = pos.X / slider.ActualWidth;
                slider.Value = slider.Minimum + (ratio * (slider.Maximum - slider.Minimum));
            }
        }

        /// <summary>
        /// Recalculates the estimated bead count based on alpha threshold.
        /// </summary>
        private void RecalculateBeads()
        {
            if (_currentImage == null) return;
            
            try
            {
                int threshold = (int)AlphaThresholdSlider.Value;
                int count = 0;
                
                // Count pixels with alpha >= threshold
                for (int i = 3; i < _currentPixels!.Length; i += 4)
                {
                    if (_currentPixels[i] >= threshold)
                        count++;
                }
                
                EstimatedBeadsText.Text = $"{count:N0}";
            }
            catch
            {
                EstimatedBeadsText.Text = Loc.T("messages.no_data");
            }
        }

        #endregion

        // ============================================================================
        // COLOR PULSE ANIMATION
        // ============================================================================
        #region Color Pulse

        /// <summary>
        /// Creates a pulsing animation on a color border to draw attention to it.
        /// Used when right-clicking on a pixel to show which color it corresponds to.
        /// </summary>
        private void PulseColorBorder(Border border)
        {
            // Save original appearance
            var originalBrush = border.BorderBrush;
            var originalThickness = border.BorderThickness;
            var originalOpacity = border.Opacity;
            
            // Apply pulse appearance
            border.BorderBrush = new SolidColorBrush(Colors.Yellow);
            border.BorderThickness = new Thickness(3);
            
            // Create pulse animation timer
            var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(150) };
            int pulseCount = 0;
            
            timer.Tick += (s, e) =>
            {
                pulseCount++;
                
                if (pulseCount >= 6)
                {
                    // Animation complete - restore original appearance
                    timer.Stop();
                    border.Opacity = originalOpacity; // Ensure visible state
                    border.BorderBrush = originalBrush;
                    border.BorderThickness = originalThickness;
                }
                else
                {
                    // Alternate between visible and faded
                    border.Opacity = pulseCount % 2 == 0 ? 1.0 : 0.3;
                }
            };
            
            timer.Start();
        }

        #endregion

        // ============================================================================
        // IMAGE LOADING
        // ============================================================================
        #region Image Loading

        /// <summary>
        /// Loads an image file and displays it in the viewport.
        /// </summary>
        private void LoadImage(string filePath)
        {
            try
            {
                // Load the image
                var bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.UriSource = new Uri(filePath);
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.EndInit();
                bitmap.Freeze();

                // Store image data
                _currentImage = bitmap;
                _currentImagePath = filePath;
                _imageWidth = bitmap.PixelWidth;
                _imageHeight = bitmap.PixelHeight;

                // Cache pixel data for color picking (convert to BGRA32 format)
                var converted = new FormatConvertedBitmap(bitmap, PixelFormats.Bgra32, null, 0);
                _currentStride = _imageWidth * 4;
                _currentPixels = new byte[_imageHeight * _currentStride];
                converted.CopyPixels(_currentPixels, _currentStride, 0);

                // Set up the original image layer
                OriginalImageLayer.Source = bitmap;
                OriginalImageLayer.Width = _imageWidth;
                OriginalImageLayer.Height = _imageHeight;

                // Reset generated image state
                GeneratedImageLayer.Source = null;
                GeneratedImageLayer.Visibility = Visibility.Collapsed;
                _hasGeneratedImage = false;
                _currentPattern = null;
                ShowGeneratedImageCheckbox.IsChecked = false;
                ShowGeneratedImageCheckbox.IsEnabled = false;
                PatternColorsUsedText.Text = "";
                BeadsPalettePanel.Children.Clear();
                ClearHighlight();

                // Reset user image layer
                UserImageLayer.Source = null;
                UserImageLayer.Visibility = Visibility.Collapsed;
                
                // Hide placeholder
                PlaceholderPanel.Visibility = Visibility.Collapsed;

                // Fit image to window and show grid if enabled
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    FitToWindow();
                    if (_isGridVisible)
                    {
                        BuildGridLayer();
                        GridLayer.Visibility = Visibility.Visible;
                    }
                }), DispatcherPriority.Loaded);

                // Update info panels
                UpdateImageInfo(filePath, bitmap);
                AnalyzeColors(bitmap);
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    Loc.T("messages.error_loading_image", ex.Message),
                    Loc.T("messages.error"),
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// Loads a folder into the file tree view.
        /// </summary>
        private void LoadFolder(string folderPath)
        {
            _lastOpenedDirectory = folderPath;
            FileTreeView.Items.Clear();
            SearchTextBox.Text = "";
            
            var rootItem = CreateTreeItem(folderPath, true);
            rootItem.IsExpanded = true;
            FileTreeView.Items.Add(rootItem);
            PopulateFolder(rootItem, folderPath);
        }

        /// <summary>
        /// Creates a tree view item for a file or folder.
        /// </summary>
        private TreeViewItem CreateTreeItem(string path, bool isFolder)
        {
            var item = new TreeViewItem { Tag = path };
            
            var panel = new StackPanel { Orientation = Orientation.Horizontal };
            panel.Children.Add(new TextBlock
            {
                Text = isFolder ? "📁" : "🖼",
                Margin = new Thickness(0, 0, 6, 0),
                VerticalAlignment = VerticalAlignment.Center,
                Foreground = Brushes.White
            });
            panel.Children.Add(new TextBlock
            {
                Text = IO.Path.GetFileName(path),
                VerticalAlignment = VerticalAlignment.Center,
                Foreground = Brushes.White
            });
            
            item.Header = panel;
            return item;
        }

        /// <summary>
        /// Populates a tree view item with its subfolder and file children.
        /// </summary>
        private void PopulateFolder(TreeViewItem parent, string path)
        {
            try
            {
                // Add subfolders
                foreach (var dir in IO.Directory.GetDirectories(path))
                {
                    var dirItem = CreateTreeItem(dir, true);
                    parent.Items.Add(dirItem);
                    
                    // Add placeholder for lazy loading if folder has contents
                    if (HasSubItems(dir))
                    {
                        dirItem.Items.Add(new TreeViewItem { Header = "Loading..." });
                        dirItem.Expanded += FolderExpanded;
                    }
                }
                
                // Add image files
                foreach (var file in IO.Directory.GetFiles(path))
                {
                    if (_supportedExtensions.Contains(IO.Path.GetExtension(file).ToLower()))
                    {
                        parent.Items.Add(CreateTreeItem(file, false));
                    }
                }
            }
            catch
            {
                // Ignore access errors
            }
        }

        /// <summary>
        /// Checks if a folder contains any subfolders or supported image files.
        /// </summary>
        private bool HasSubItems(string path)
        {
            try
            {
                return IO.Directory.GetDirectories(path).Any() ||
                       IO.Directory.GetFiles(path).Any(f => _supportedExtensions.Contains(IO.Path.GetExtension(f).ToLower()));
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Handles folder expansion in the tree view (lazy loading).
        /// </summary>
        private void FolderExpanded(object sender, RoutedEventArgs e)
        {
            if (sender is TreeViewItem item && item.Tag is string path)
            {
                item.Items.Clear();
                PopulateFolder(item, path);
                item.Expanded -= FolderExpanded;
            }
        }

        /// <summary>
        /// Handles tree view item selection - loads the selected image.
        /// </summary>
        private void FileTreeView_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            if (e.NewValue is TreeViewItem item && item.Tag is string path)
            {
                if (IO.File.Exists(path) && _supportedExtensions.Contains(IO.Path.GetExtension(path).ToLower()))
                {
                    LoadImage(path);
                }
            }
        }

        /// <summary>
        /// Handles search text changes - filters the file tree view.
        /// </summary>
        private void SearchTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            var text = SearchTextBox.Text?.Trim().ToLower() ?? "";
            
            foreach (TreeViewItem item in FileTreeView.Items)
            {
                if (string.IsNullOrEmpty(text))
                    ResetVisibility(item);
                else
                    FilterItem(item, text);
            }
        }

        /// <summary>
        /// Resets visibility of all tree view items.
        /// </summary>
        private void ResetVisibility(TreeViewItem item)
        {
            item.Visibility = Visibility.Visible;
            foreach (TreeViewItem child in item.Items)
                ResetVisibility(child);
        }

        /// <summary>
        /// Filters a tree view item based on search text.
        /// Returns true if the item or any children match.
        /// </summary>
        private bool FilterItem(TreeViewItem item, string text)
        {
            bool hasVisibleChild = false;
            
            foreach (TreeViewItem child in item.Items)
            {
                if (FilterItem(child, text))
                    hasVisibleChild = true;
            }
            
            var path = item.Tag as string;
            var name = path != null ? IO.Path.GetFileName(path).ToLower() : "";
            bool matches = name.Contains(text) || hasVisibleChild;
            
            item.Visibility = matches ? Visibility.Visible : Visibility.Collapsed;
            
            if (hasVisibleChild)
                item.IsExpanded = true;
            
            return matches;
        }

        #endregion

        // ============================================================================
        // VIEWPORT HANDLING
        // ============================================================================
        #region Viewport

        /// <summary>
        /// Handles viewport resize - centers the placeholder.
        /// </summary>
        private void Viewport_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            CenterPlaceholder();
        }

        /// <summary>
        /// Centers the placeholder panel in the viewport.
        /// </summary>
        private void CenterPlaceholder()
        {
            if (ViewportCanvas.ActualWidth > 0)
            {
                Canvas.SetLeft(PlaceholderPanel, (ViewportCanvas.ActualWidth - 300) / 2);
                Canvas.SetTop(PlaceholderPanel, (ViewportCanvas.ActualHeight - 150) / 2);
            }
        }

        /// <summary>
        /// Handles left mouse button down - starts panning.
        /// </summary>
        private void Viewport_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (_currentImage != null)
            {
                _isPanning = true;
                _lastMousePosition = e.GetPosition(ViewportCanvas);
                ViewportCanvas.Cursor = Cursors.Hand;
                ViewportCanvas.CaptureMouse();
            }
        }

        /// <summary>
        /// Handles left mouse button up - ends panning.
        /// </summary>
        private void Viewport_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            _isPanning = false;
            ViewportCanvas.Cursor = Cursors.Arrow;
            ViewportCanvas.ReleaseMouseCapture();
        }

        /// <summary>
        /// Handles right mouse button down - color picking and pulse.
        /// Shows which color in the palette corresponds to the clicked pixel.
        /// </summary>
        private void Viewport_MouseRightButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (_currentImage == null || _currentPixels == null) return;
            
            // Get pixel coordinates
            var pos = e.GetPosition(LayerContainer);
            int x = (int)pos.X;
            int y = (int)pos.Y;
            
            // Check bounds
            if (x < 0 || x >= _imageWidth || y < 0 || y >= _imageHeight) return;
            
            // Get pixel color from cached data
            int idx = y * _currentStride + x * 4;
            if (idx + 3 >= _currentPixels.Length) return;
            
            byte b = _currentPixels[idx];
            byte g = _currentPixels[idx + 1];
            byte r = _currentPixels[idx + 2];
            byte a = _currentPixels[idx + 3];
            
            // Ignore transparent pixels
            if (a < AlphaThresholdSlider.Value) return;
            
            // Determine which palette to pulse based on current view state
            if (_hasGeneratedImage && ShowGeneratedImageCheckbox.IsChecked == true && _currentPattern != null)
            {
                // Viewing generated image - pulse in beads palette
                foreach (var patternColor in _currentPattern.Colors)
                {
                    if (patternColor.Pixels.Any(p => p.X == x && p.Y == y))
                    {
                        if (_beadsColorBorders.TryGetValue(patternColor.ColorId, out var border))
                        {
                            PulseColorBorder(border);
                        }
                        break;
                    }
                }
            }
            else
            {
                // Viewing original image - pulse in original palette
                int colorKey = (r << 16) | (g << 8) | b;
                if (_originalColorBorders.TryGetValue(colorKey, out var border))
                {
                    PulseColorBorder(border);
                }
            }
        }

        /// <summary>
        /// Handles mouse movement - updates pan position if panning.
        /// </summary>
        private void Viewport_MouseMove(object sender, MouseEventArgs e)
        {
            if (_isPanning && _currentImage != null)
            {
                var pos = e.GetPosition(ViewportCanvas);
                LayerTranslate.X += pos.X - _lastMousePosition.X;
                LayerTranslate.Y += pos.Y - _lastMousePosition.Y;
                _lastMousePosition = pos;
            }
        }

        /// <summary>
        /// Handles mouse wheel - zooms in/out when Ctrl is held.
        /// </summary>
        private void Viewport_MouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (_currentImage != null && Keyboard.Modifiers == ModifierKeys.Control)
            {
                var mousePos = e.GetPosition(ViewportCanvas);
                double newZoom = _zoom * (e.Delta > 0 ? ZoomFactor : 1.0 / ZoomFactor);
                ZoomAtPoint(newZoom, mousePos.X, mousePos.Y);
                e.Handled = true;
            }
        }

        /// <summary>
        /// Zooms to a specific level, keeping a point fixed in screen space.
        /// </summary>
        private void ZoomAtPoint(double newZoom, double pivotX, double pivotY)
        {
            if (_currentImage == null) return;
            
            // Clamp zoom to valid range
            newZoom = Math.Max(MinZoom, Math.Min(MaxZoom, newZoom));
            
            // Calculate image position under pivot point
            double tx = LayerTranslate.X;
            double ty = LayerTranslate.Y;
            double imgX = (pivotX - tx) / _zoom;
            double imgY = (pivotY - ty) / _zoom;
            
            // Apply new zoom
            _zoom = newZoom;
            LayerScale.ScaleX = _zoom;
            LayerScale.ScaleY = _zoom;
            
            // Adjust translation to keep pivot point fixed
            LayerTranslate.X = pivotX - imgX * _zoom;
            LayerTranslate.Y = pivotY - imgY * _zoom;
            
            UpdateZoomDisplay();
            UpdateGridLineThickness();
        }

        /// <summary>
        /// Zooms to a specific level, centered on the viewport.
        /// </summary>
        private void ZoomAtCenter(double newZoom)
        {
            if (_currentImage != null)
            {
                ZoomAtPoint(newZoom, ViewportCanvas.ActualWidth / 2, ViewportCanvas.ActualHeight / 2);
            }
        }

        /// <summary>
        /// Centers the image layers in the viewport.
        /// </summary>
        private void CenterLayers()
        {
            if (_currentImage == null) return;
            
            LayerScale.ScaleX = _zoom;
            LayerScale.ScaleY = _zoom;
            LayerTranslate.X = (ViewportCanvas.ActualWidth - _imageWidth * _zoom) / 2;
            LayerTranslate.Y = (ViewportCanvas.ActualHeight - _imageHeight * _zoom) / 2;
            
            UpdateGridLineThickness();
        }

        /// <summary>
        /// Fits the image to the viewport while maintaining aspect ratio.
        /// </summary>
        private void FitToWindow()
        {
            if (_currentImage == null) return;
            
            double viewportWidth = ViewportCanvas.ActualWidth;
            double viewportHeight = ViewportCanvas.ActualHeight;
            
            if (viewportWidth <= 0 || viewportHeight <= 0) return;
            
            // Calculate zoom to fit with 10% margin
            _zoom = Math.Min(
                Math.Min(viewportWidth / _imageWidth, viewportHeight / _imageHeight) * 0.9,
                MaxZoom);
            
            CenterLayers();
            UpdateZoomDisplay();
        }

        /// <summary>
        /// Updates the zoom level display in the toolbar.
        /// </summary>
        private void UpdateZoomDisplay()
        {
            ZoomLevelText.Text = $"{_zoom * 100:F0}%";
        }

        #endregion

        // ============================================================================
        // GRID LAYER
        // ============================================================================
        #region Grid

        /// <summary>
        /// Builds the pixel grid overlay layer.
        /// </summary>
        private void BuildGridLayer()
        {
            if (_currentImage == null) return;
            
            GridLayer.Children.Clear();
            var brush = new SolidColorBrush(_gridColor);
            
            // Add vertical lines
            for (int x = 0; x <= _imageWidth; x++)
            {
                var line = new Line
                {
                    X1 = x, Y1 = 0,
                    X2 = x, Y2 = _imageHeight,
                    Stroke = brush,
                    StrokeThickness = 1.0 / _zoom,
                    SnapsToDevicePixels = true
                };
                line.SetValue(RenderOptions.EdgeModeProperty, EdgeMode.Aliased);
                GridLayer.Children.Add(line);
            }
            
            // Add horizontal lines
            for (int y = 0; y <= _imageHeight; y++)
            {
                var line = new Line
                {
                    X1 = 0, Y1 = y,
                    X2 = _imageWidth, Y2 = y,
                    Stroke = brush,
                    StrokeThickness = 1.0 / _zoom,
                    SnapsToDevicePixels = true
                };
                line.SetValue(RenderOptions.EdgeModeProperty, EdgeMode.Aliased);
                GridLayer.Children.Add(line);
            }
        }

        /// <summary>
        /// Updates grid line thickness based on current zoom level.
        /// </summary>
        private void UpdateGridLineThickness()
        {
            if (!_isGridVisible) return;
            
            foreach (var child in GridLayer.Children)
            {
                if (child is Line line)
                {
                    line.StrokeThickness = 1.0 / _zoom;
                }
            }
        }

        #endregion

        // ============================================================================
        // IMAGE INFO DISPLAY
        // ============================================================================
        #region Image Info

        /// <summary>
        /// Updates the image and file info panels with data from the loaded image.
        /// </summary>
        private void UpdateImageInfo(string path, BitmapSource bitmap)
        {
            var fileInfo = new IO.FileInfo(path);
            
            // Image info section
            FileNameText.Text = fileInfo.Name;
            DimensionsText.Text = $"{bitmap.PixelWidth} × {bitmap.PixelHeight} px";
            
            // File info section
            FileNameText2.Text = fileInfo.Name;
            FilePathText.Text = fileInfo.DirectoryName ?? Loc.T("messages.no_data");
            DimensionsText2.Text = $"{bitmap.PixelWidth} × {bitmap.PixelHeight} px";
            TotalPixelsText.Text = $"{bitmap.PixelWidth * bitmap.PixelHeight:N0}";
            FileSizeText.Text = FormatFileSize(fileInfo.Length);
            
            RecalculateBeads();
        }

        /// <summary>
        /// Formats a file size in bytes to a human-readable string.
        /// </summary>
        private string FormatFileSize(long bytes)
        {
            string[] units = { "B", "KB", "MB", "GB" };
            int unitIndex = 0;
            double size = bytes;
            
            while (size >= 1024 && unitIndex < units.Length - 1)
            {
                size /= 1024;
                unitIndex++;
            }
            
            return $"{size:0.##} {units[unitIndex]}";
        }

        /// <summary>
        /// Analyzes the colors in the loaded image and displays the palette.
        /// </summary>
        private void AnalyzeColors(BitmapSource bitmap)
        {
            try
            {
                // Count occurrences of each color
                var colorCounts = new Dictionary<int, int>();
                
                for (int i = 0; i < _currentPixels!.Length; i += 4)
                {
                    // Only count non-transparent pixels
                    if (_currentPixels[i + 3] > 0)
                    {
                        int key = (_currentPixels[i + 2] << 16) | (_currentPixels[i + 1] << 8) | _currentPixels[i];
                        colorCounts[key] = colorCounts.GetValueOrDefault(key, 0) + 1;
                    }
                }
                
                UniqueColorsText.Text = $"{colorCounts.Count:N0}";
                DisplayOriginalPalette(colorCounts);
            }
            catch
            {
                UniqueColorsText.Text = Loc.T("messages.no_data");
                ColorPalettePanel.Children.Clear();
            }
        }

        /// <summary>
        /// Displays the original image color palette (top 20 colors by pixel count).
        /// </summary>
        private void DisplayOriginalPalette(Dictionary<int, int> colorCounts)
        {
            ColorPalettePanel.Children.Clear();
            _originalColorBorders.Clear();
            
            // Show top 20 colors sorted by pixel count
            foreach (var kvp in colorCounts.OrderByDescending(x => x.Value).Take(20))
            {
                int key = kvp.Key;
                byte r = (byte)((key >> 16) & 0xFF);
                byte g = (byte)((key >> 8) & 0xFF);
                byte b = (byte)(key & 0xFF);
                
                var border = new Border
                {
                    Width = 24,
                    Height = 24,
                    Margin = new Thickness(2),
                    Background = new SolidColorBrush(Color.FromRgb(r, g, b)),
                    BorderBrush = Brushes.White,
                    BorderThickness = new Thickness(2),
                    CornerRadius = new CornerRadius(3),
                    ToolTip = Loc.T("tooltips.color_pixels", $"{r:X2}{g:X2}{b:X2}", kvp.Value)
                };
                
                _originalColorBorders[key] = border;
                ColorPalettePanel.Children.Add(border);
            }
        }

        #endregion

        // ============================================================================
        // KEYBOARD SHORTCUTS
        // ============================================================================
        #region Keyboard

        /// <summary>
        /// Handles keyboard shortcuts.
        /// </summary>
        private void MainWindow_KeyDown(object sender, KeyEventArgs e)
        {
            if (Keyboard.Modifiers == ModifierKeys.Control)
            {
                switch (e.Key)
                {
                    case Key.O:
                        OpenFile_Click(sender, e);
                        e.Handled = true;
                        break;
                    case Key.D0:
                    case Key.NumPad0:
                        ResetView_Click(sender, e);
                        e.Handled = true;
                        break;
                    case Key.OemPlus:
                    case Key.Add:
                        ZoomIn_Click(sender, e);
                        e.Handled = true;
                        break;
                    case Key.OemMinus:
                    case Key.Subtract:
                        ZoomOut_Click(sender, e);
                        e.Handled = true;
                        break;
                    case Key.G:
                        ShowGrid_Click(sender, e);
                        e.Handled = true;
                        break;
                }
            }
            else if (e.Key == Key.Escape)
            {
                // Clear highlight on Escape
                ClearHighlight();
            }
        }

        #endregion
    }

    // ============================================================================
    // SETTINGS DATA CLASSES
    // ============================================================================
    #region Settings Classes

    /// <summary>Root settings object containing all application settings.</summary>
    public class AppSettings
    {
        public CanvasSettings? Canvas { get; set; }
        public GridSettings? Grid { get; set; }
        public HighlightSettings? Highlight { get; set; }
        public AppearanceSettings? Appearance { get; set; }
        public ViewSettings? View { get; set; }
        public FilesSettings? Files { get; set; }
        public WindowSettings? Window { get; set; }
        public BeadsSettings? Beads { get; set; }
        public string? Language { get; set; }
    }

    /// <summary>Canvas appearance settings.</summary>
    public class CanvasSettings
    {
        public string BackgroundColor { get; set; } = "#FFFFFF";
    }

    /// <summary>Pixel grid overlay settings.</summary>
    public class GridSettings
    {
        public string Color { get; set; } = "#808080";
        public int Opacity { get; set; } = 150;
        public bool Visible { get; set; } = false;
    }

    /// <summary>Color highlight settings.</summary>
    public class HighlightSettings
    {
        public string Color { get; set; } = "#FFFF00";
        public int Opacity { get; set; } = 200;
    }
    
    /// <summary>UI appearance settings.</summary>
    public class AppearanceSettings
    {
        public string AccentColor { get; set; } = "#B70074";
    }

    /// <summary>Viewport settings.</summary>
    public class ViewSettings
    {
        public double LastZoom { get; set; } = 1.0;
    }

    /// <summary>File handling settings.</summary>
    public class FilesSettings
    {
        public string LastOpenedDirectory { get; set; } = "";
        public List<string> RecentFiles { get; set; } = new List<string>();
    }

    /// <summary>Window position and size settings.</summary>
    public class WindowSettings
    {
        public int Width { get; set; } = 1280;
        public int Height { get; set; } = 720;
        public bool Maximized { get; set; } = false;
    }

    /// <summary>Bead pattern generation settings.</summary>
    public class BeadsSettings
    {
        public int AlphaThreshold { get; set; } = 200;
        public int ColorCompression { get; set; } = 1;
        public int ColorSpace { get; set; } = 0; // 0 = RGB, 1 = Lab
    }

    #endregion
}
