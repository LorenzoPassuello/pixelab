using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace Pixelab
{
    public partial class ColorPickerWindow : Window
    {
        private bool _isInitialized = false;
        private bool _isUpdating = false;
        private bool _isDragging = false;
        private Border? _activeSlider = null;
        
        private byte _r, _g, _b;
        private double _h, _s, _v;
        
        private Color _originalColor = Colors.Red;
        public Color SelectedColor { get; private set; } = Colors.Red;

        public ColorPickerWindow()
        {
            InitializeComponent();
            Loaded += ColorPickerWindow_Loaded;
            MouseMove += Window_MouseMove;
            MouseLeftButtonUp += Window_MouseLeftButtonUp;
        }

        public ColorPickerWindow(Color initialColor) : this()
        {
            _originalColor = initialColor;
            SelectedColor = initialColor;
        }

        private void ColorPickerWindow_Loaded(object sender, RoutedEventArgs e)
        {
            // Initialize from original color
            _r = _originalColor.R;
            _g = _originalColor.G;
            _b = _originalColor.B;
            
            OriginalColorPreview.Background = new SolidColorBrush(_originalColor);
            NewColorPreview.Background = new SolidColorBrush(_originalColor);
            
            // Delay slider positioning to ensure controls have their final dimensions
            Dispatcher.BeginInvoke(new Action(() =>
            {
                RgbToHsv(_r, _g, _b, out _h, out _s, out _v);
                _isInitialized = true;
                _isUpdating = true;
                UpdateAllUI();
                _isUpdating = false;
            }), System.Windows.Threading.DispatcherPriority.Render);
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
            
            var pos = e.GetPosition(_activeSlider);
            
            if (_activeSlider == RSliderBorder) UpdateRFromMouse(pos.X);
            else if (_activeSlider == GSliderBorder) UpdateGFromMouse(pos.X);
            else if (_activeSlider == BSliderBorder) UpdateBFromMouse(pos.X);
            else if (_activeSlider == HSliderBorder) UpdateHFromMouse(pos.X);
            else if (_activeSlider == SSliderBorder) UpdateSFromMouse(pos.X);
            else if (_activeSlider == VSliderBorder) UpdateVFromMouse(pos.X);
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
            DialogResult = true;
            Close();
        }
    }
}
