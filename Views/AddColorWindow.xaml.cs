using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace Pixelab
{
    public partial class AddColorWindow : Window
    {
        private static LocalizationManager Loc => LocalizationManager.Instance;
        
        private bool _isInitialized = false;
        private bool _isUpdating = false;
        private bool _isDragging = false;
        private Border? _activeSlider = null;
        
        private byte _r = 255, _g = 0, _b = 0;
        private double _h = 0, _s = 100, _v = 100;
        
        public Color SelectedColor { get; private set; } = Colors.Red;
        public string ColorId { get; private set; } = "";
        public string ColorName { get; private set; } = "";
        public string GroupId { get; private set; } = "custom";
        public string? NewGroupId { get; private set; }
        public string? NewGroupName { get; private set; }
        
        private List<PatternGenerator.ColorGroup> _groups = new();
        private Dictionary<string, int> _groupColorCounts = new();
        
        private readonly Func<List<PatternGenerator.ColorGroup>>? _getGroups;
        private readonly Func<string, int>? _getNextColorNumber;
        private readonly bool _allowNewGroup;

        public AddColorWindow(
            Func<List<PatternGenerator.ColorGroup>> getGroups,
            Func<string, int> getNextColorNumber,
            bool allowNewGroup = true)
        {
            InitializeComponent();
            _getGroups = getGroups;
            _getNextColorNumber = getNextColorNumber;
            _allowNewGroup = allowNewGroup;
            
            Loaded += AddColorWindow_Loaded;
            MouseMove += Window_MouseMove;
            MouseLeftButtonUp += Window_MouseLeftButtonUp;
        }

        private void AddColorWindow_Loaded(object sender, RoutedEventArgs e)
        {
            // Apply localization
            ApplyLocalization();
            
            // Load groups
            LoadGroups();
            
            // Delay initialization to ensure controls have their final dimensions
            Dispatcher.BeginInvoke(new Action(() =>
            {
                _isInitialized = true;
                RgbToHsv(_r, _g, _b, out _h, out _s, out _v);
                UpdateAllUI();
            }), System.Windows.Threading.DispatcherPriority.Render);
        }
        
        private void ApplyLocalization()
        {
            Title = Loc.T("add_color.title");
            NewColorLabel.Text = Loc.T("add_color.new");
            ColorDetailsHeader.Text = Loc.T("add_color.details");
            GroupLabel.Text = Loc.T("add_color.group");
            NewGroupIdLabel.Text = Loc.T("add_color.new_group_id");
            NewGroupNameLabel.Text = Loc.T("add_color.new_group_name");
            ColorIdLabel.Text = Loc.T("add_color.color_id");
            ColorNameLabel.Text = Loc.T("add_color.color_name");
            ColorNameHint.Text = Loc.T("add_color.color_name_hint");
            CancelButton.Content = Loc.T("buttons.cancel");
            OKButton.Content = Loc.T("add_color.add_button");
        }
        
        private void LoadGroups()
        {
            if (_getGroups == null) return;
            
            _groups = _getGroups();
            GroupComboBox.Items.Clear();
            
            // Add existing groups
            foreach (var group in _groups)
            {
                GroupComboBox.Items.Add(new ComboBoxItem 
                { 
                    Content = group.Name, 
                    Tag = group.GroupId 
                });
            }
            
            // Add "New group" option
            if (_allowNewGroup)
                GroupComboBox.Items.Add(new ComboBoxItem
                {
                    Content = Loc.T("add_color.new_group"),
                    Tag = "__new__"
                });
            
            // Select "custom" by default, or first group if custom doesn't exist
            int customIndex = -1;
            for (int i = 0; i < GroupComboBox.Items.Count; i++)
            {
                if (GroupComboBox.Items[i] is ComboBoxItem item && (string)item.Tag == "custom")
                {
                    customIndex = i;
                    break;
                }
            }
            GroupComboBox.SelectedIndex = customIndex >= 0 ? customIndex : 0;
            
            UpdateColorId();
        }
        
        private void GroupComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (GroupComboBox.SelectedItem is ComboBoxItem item)
            {
                string tag = (string)item.Tag;
                if (tag == "__new__")
                {
                    NewGroupPanel.Visibility = Visibility.Visible;
                    GroupId = "";
                }
                else
                {
                    NewGroupPanel.Visibility = Visibility.Collapsed;
                    GroupId = tag;
                    UpdateColorId();
                }
            }
        }
        
        private void UpdateColorId()
        {
            if (!_isInitialized || _getNextColorNumber == null) return;
            
            string groupId = GroupId;
            if (string.IsNullOrEmpty(groupId) && NewGroupPanel.Visibility == Visibility.Visible)
            {
                groupId = NewGroupIdTextBox.Text;
            }
            
            if (string.IsNullOrEmpty(groupId)) groupId = "custom";
            
            int nextNum = _getNextColorNumber(groupId);
            string prefix = groupId.Length > 5 ? groupId.Substring(0, 5).ToUpper() : groupId.ToUpper();
            ColorIdTextBox.Text = $"{prefix}_{nextNum:D3}";
        }

        #region Slider Mouse Handlers

        private void RSlider_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (!_isInitialized) return;
            _isDragging = true;
            _activeSlider = RSliderBorder;
            RSliderBorder.CaptureMouse();
            UpdateRFromMouse(e.GetPosition(RSliderBorder).X);
        }

        private void GSlider_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (!_isInitialized) return;
            _isDragging = true;
            _activeSlider = GSliderBorder;
            GSliderBorder.CaptureMouse();
            UpdateGFromMouse(e.GetPosition(GSliderBorder).X);
        }

        private void BSlider_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (!_isInitialized) return;
            _isDragging = true;
            _activeSlider = BSliderBorder;
            BSliderBorder.CaptureMouse();
            UpdateBFromMouse(e.GetPosition(BSliderBorder).X);
        }

        private void HSlider_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (!_isInitialized) return;
            _isDragging = true;
            _activeSlider = HSliderBorder;
            HSliderBorder.CaptureMouse();
            UpdateHFromMouse(e.GetPosition(HSliderBorder).X);
        }

        private void SSlider_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (!_isInitialized) return;
            _isDragging = true;
            _activeSlider = SSliderBorder;
            SSliderBorder.CaptureMouse();
            UpdateSFromMouse(e.GetPosition(SSliderBorder).X);
        }

        private void VSlider_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (!_isInitialized) return;
            _isDragging = true;
            _activeSlider = VSliderBorder;
            VSliderBorder.CaptureMouse();
            UpdateVFromMouse(e.GetPosition(VSliderBorder).X);
        }

        private void Window_MouseMove(object sender, MouseEventArgs e)
        {
            if (!_isDragging || _activeSlider == null) return;
            
            double x = e.GetPosition(_activeSlider).X;
            
            if (_activeSlider == RSliderBorder) UpdateRFromMouse(x);
            else if (_activeSlider == GSliderBorder) UpdateGFromMouse(x);
            else if (_activeSlider == BSliderBorder) UpdateBFromMouse(x);
            else if (_activeSlider == HSliderBorder) UpdateHFromMouse(x);
            else if (_activeSlider == SSliderBorder) UpdateSFromMouse(x);
            else if (_activeSlider == VSliderBorder) UpdateVFromMouse(x);
        }

        private void Window_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (_isDragging && _activeSlider != null)
            {
                _activeSlider.ReleaseMouseCapture();
                _isDragging = false;
                _activeSlider = null;
            }
        }

        private void UpdateRFromMouse(double x)
        {
            double ratio = Math.Max(0, Math.Min(1, x / RSliderBorder.ActualWidth));
            _r = (byte)(ratio * 255);
            _isUpdating = true;
            RgbToHsv(_r, _g, _b, out _h, out _s, out _v);
            UpdateAllUI();
            _isUpdating = false;
        }

        private void UpdateGFromMouse(double x)
        {
            double ratio = Math.Max(0, Math.Min(1, x / GSliderBorder.ActualWidth));
            _g = (byte)(ratio * 255);
            _isUpdating = true;
            RgbToHsv(_r, _g, _b, out _h, out _s, out _v);
            UpdateAllUI();
            _isUpdating = false;
        }

        private void UpdateBFromMouse(double x)
        {
            double ratio = Math.Max(0, Math.Min(1, x / BSliderBorder.ActualWidth));
            _b = (byte)(ratio * 255);
            _isUpdating = true;
            RgbToHsv(_r, _g, _b, out _h, out _s, out _v);
            UpdateAllUI();
            _isUpdating = false;
        }

        private void UpdateHFromMouse(double x)
        {
            double ratio = Math.Max(0, Math.Min(1, x / HSliderBorder.ActualWidth));
            _h = ratio * 360;
            _isUpdating = true;
            HsvToRgb(_h, _s, _v, out _r, out _g, out _b);
            UpdateAllUI();
            _isUpdating = false;
        }

        private void UpdateSFromMouse(double x)
        {
            double ratio = Math.Max(0, Math.Min(1, x / SSliderBorder.ActualWidth));
            _s = ratio * 100;
            _isUpdating = true;
            HsvToRgb(_h, _s, _v, out _r, out _g, out _b);
            UpdateAllUI();
            _isUpdating = false;
        }

        private void UpdateVFromMouse(double x)
        {
            double ratio = Math.Max(0, Math.Min(1, x / VSliderBorder.ActualWidth));
            _v = ratio * 100;
            _isUpdating = true;
            HsvToRgb(_h, _s, _v, out _r, out _g, out _b);
            UpdateAllUI();
            _isUpdating = false;
        }

        #endregion

        #region TextBox Handlers

        private void RgbTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (!_isInitialized || _isUpdating) return;
            
            if (byte.TryParse(RTextBox.Text, out byte r) &&
                byte.TryParse(GTextBox.Text, out byte g) &&
                byte.TryParse(BTextBox.Text, out byte b))
            {
                _r = r; _g = g; _b = b;
                _isUpdating = true;
                RgbToHsv(_r, _g, _b, out _h, out _s, out _v);
                UpdateAllUI();
                _isUpdating = false;
            }
        }

        private void HexTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (!_isInitialized || _isUpdating) return;
            
            string hex = HexTextBox.Text.Trim();
            if (!hex.StartsWith("#")) hex = "#" + hex;
            
            try
            {
                if (hex.Length == 7)
                {
                    Color color = (Color)ColorConverter.ConvertFromString(hex);
                    _r = color.R; _g = color.G; _b = color.B;
                    _isUpdating = true;
                    RgbToHsv(_r, _g, _b, out _h, out _s, out _v);
                    UpdateAllUI();
                    _isUpdating = false;
                }
            }
            catch { }
        }

        private void HsvTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (!_isInitialized || _isUpdating) return;
            
            if (double.TryParse(HTextBox.Text, out double h) &&
                double.TryParse(STextBox.Text, out double s) &&
                double.TryParse(VTextBox.Text, out double v))
            {
                _h = Math.Max(0, Math.Min(360, h));
                _s = Math.Max(0, Math.Min(100, s));
                _v = Math.Max(0, Math.Min(100, v));
                _isUpdating = true;
                HsvToRgb(_h, _s, _v, out _r, out _g, out _b);
                UpdateAllUI();
                _isUpdating = false;
            }
        }

        #endregion

        #region UI Update

        private void UpdateAllUI()
        {
            SelectedColor = Color.FromRgb(_r, _g, _b);
            NewColorPreview.Background = new SolidColorBrush(SelectedColor);
            
            if (!RTextBox.IsFocused) RTextBox.Text = _r.ToString();
            if (!GTextBox.IsFocused) GTextBox.Text = _g.ToString();
            if (!BTextBox.IsFocused) BTextBox.Text = _b.ToString();
            if (!HexTextBox.IsFocused) HexTextBox.Text = $"#{_r:X2}{_g:X2}{_b:X2}";
            if (!HTextBox.IsFocused) HTextBox.Text = ((int)_h).ToString();
            if (!STextBox.IsFocused) STextBox.Text = ((int)_s).ToString();
            if (!VTextBox.IsFocused) VTextBox.Text = ((int)_v).ToString();
            
            UpdateSliderThumb(RSliderCanvas, RSliderThumb, RSliderBorder, _r / 255.0);
            UpdateSliderThumb(GSliderCanvas, GSliderThumb, GSliderBorder, _g / 255.0);
            UpdateSliderThumb(BSliderCanvas, BSliderThumb, BSliderBorder, _b / 255.0);
            UpdateSliderThumb(HSliderCanvas, HSliderThumb, HSliderBorder, _h / 360.0);
            UpdateSliderThumb(SSliderCanvas, SSliderThumb, SSliderBorder, _s / 100.0);
            UpdateSliderThumb(VSliderCanvas, VSliderThumb, VSliderBorder, _v / 100.0);
            
            UpdateSliderGradients();
        }

        private void UpdateSliderThumb(Canvas canvas, Border thumb, Border sliderBorder, double ratio)
        {
            if (sliderBorder.ActualWidth > 0)
            {
                double x = ratio * (sliderBorder.ActualWidth - thumb.Width);
                Canvas.SetLeft(thumb, Math.Max(0, x));
            }
        }

        private void UpdateSliderGradients()
        {
            RSliderBorder.Background = new LinearGradientBrush(
                Color.FromRgb(0, _g, _b),
                Color.FromRgb(255, _g, _b), 0);
            
            GSliderBorder.Background = new LinearGradientBrush(
                Color.FromRgb(_r, 0, _b),
                Color.FromRgb(_r, 255, _b), 0);
            
            BSliderBorder.Background = new LinearGradientBrush(
                Color.FromRgb(_r, _g, 0),
                Color.FromRgb(_r, _g, 255), 0);
            
            HsvToRgb(_h, 0, _v, out byte sr0, out byte sg0, out byte sb0);
            HsvToRgb(_h, 100, _v, out byte sr100, out byte sg100, out byte sb100);
            SSliderBorder.Background = new LinearGradientBrush(
                Color.FromRgb(sr0, sg0, sb0),
                Color.FromRgb(sr100, sg100, sb100), 0);
            
            HsvToRgb(_h, _s, 0, out byte vr0, out byte vg0, out byte vb0);
            HsvToRgb(_h, _s, 100, out byte vr100, out byte vg100, out byte vb100);
            VSliderBorder.Background = new LinearGradientBrush(
                Color.FromRgb(vr0, vg0, vb0),
                Color.FromRgb(vr100, vg100, vb100), 0);
        }

        #endregion

        #region Color Conversion

        private static void RgbToHsv(byte r, byte g, byte b, out double h, out double s, out double v)
        {
            double rd = r / 255.0, gd = g / 255.0, bd = b / 255.0;
            double max = Math.Max(rd, Math.Max(gd, bd));
            double min = Math.Min(rd, Math.Min(gd, bd));
            double delta = max - min;
            
            v = max * 100;
            s = max == 0 ? 0 : (delta / max) * 100;
            
            if (delta == 0) h = 0;
            else if (max == rd) h = 60 * (((gd - bd) / delta) % 6);
            else if (max == gd) h = 60 * (((bd - rd) / delta) + 2);
            else h = 60 * (((rd - gd) / delta) + 4);
            
            if (h < 0) h += 360;
        }

        private static void HsvToRgb(double h, double s, double v, out byte r, out byte g, out byte b)
        {
            double sd = s / 100.0, vd = v / 100.0;
            double c = vd * sd;
            double x = c * (1 - Math.Abs((h / 60) % 2 - 1));
            double m = vd - c;
            
            double rd, gd, bd;
            if (h < 60) { rd = c; gd = x; bd = 0; }
            else if (h < 120) { rd = x; gd = c; bd = 0; }
            else if (h < 180) { rd = 0; gd = c; bd = x; }
            else if (h < 240) { rd = 0; gd = x; bd = c; }
            else if (h < 300) { rd = x; gd = 0; bd = c; }
            else { rd = c; gd = 0; bd = x; }
            
            r = (byte)((rd + m) * 255);
            g = (byte)((gd + m) * 255);
            b = (byte)((bd + m) * 255);
        }

        #endregion

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private void OK_Click(object sender, RoutedEventArgs e)
        {
            // Validate Color ID
            if (string.IsNullOrWhiteSpace(ColorIdTextBox.Text))
            {
                MessageBox.Show(Loc.T("add_color.error_no_color_id"), Loc.T("settings.error"), 
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            
            // Handle new group
            if (NewGroupPanel.Visibility == Visibility.Visible)
            {
                if (string.IsNullOrWhiteSpace(NewGroupIdTextBox.Text))
                {
                    MessageBox.Show(Loc.T("add_color.error_no_group_id"), Loc.T("settings.error"), 
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
                NewGroupId = NewGroupIdTextBox.Text.Trim();
                NewGroupName = string.IsNullOrWhiteSpace(NewGroupNameTextBox.Text) 
                    ? NewGroupId 
                    : NewGroupNameTextBox.Text.Trim();
                GroupId = NewGroupId;
            }
            
            ColorId = ColorIdTextBox.Text.Trim();
            ColorName = string.IsNullOrWhiteSpace(ColorNameTextBox.Text) 
                ? ColorId 
                : ColorNameTextBox.Text.Trim();
            
            DialogResult = true;
            Close();
        }
    }
}
