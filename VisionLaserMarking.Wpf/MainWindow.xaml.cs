using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
using OpenCvSharp.WpfExtensions;
using VisionLaserMarking.Calibration;
using VisionLaserMarking.Laser;
using VisionLaserMarking.Models;
using VisionLaserMarking.Vision;

namespace VisionLaserMarking.Wpf
{
    public partial class MainWindow : Window
    {
        private const int SceneWidth = 640;
        private const int SceneHeight = 380;

        private readonly PartDetector _detector = new PartDetector { DarkBackground = true, MinAreaPx = 400 };
        private readonly CoordinateCalibrator _calibrator = new CoordinateCalibrator();
        private readonly WpfLogLaserController _laser = new WpfLogLaserController();

        private List<TruePart> _trueParts = new List<TruePart>();
        private OpenCvSharp.Mat? _sceneMat;
        private List<DetectedPart> _currentParts = new List<DetectedPart>();
        private bool _manualMode;
        private int _producedCount;
        private bool _autoMarkRunning;

        public MainWindow()
        {
            InitializeComponent();

            // 카메라 픽셀좌표 <-> 가상 기계좌표(mm) 매칭점. 실제로는 9점 캘리브레이션으로 구한다.
            _calibrator.Calibrate(new[]
            {
                (Pixel: new Point2D(120, 50), Machine: new Point2D(-40.0, 30.0)),
                (Pixel: new Point2D(520, 50), Machine: new Point2D(40.0, 30.0)),
                (Pixel: new Point2D(120, 330), Machine: new Point2D(-40.0, -30.0)),
                (Pixel: new Point2D(520, 330), Machine: new Point2D(40.0, -30.0)),
            });

            _laser.Connect();
            _laser.Marked += OnLaserMarked;

            Regenerate();
        }

        private void Regenerate()
        {
            int n = (int)PartCountSlider.Value;
            _trueParts = SceneGenerator.GenerateTrueParts(n, SceneWidth, SceneHeight);
            _sceneMat?.Dispose();
            _sceneMat = SceneGenerator.RenderScene(_trueParts, SceneWidth, SceneHeight);
            SceneImage.Source = _sceneMat.ToBitmapSource();

            _currentParts = new List<DetectedPart>();
            OverlayCanvas.Children.Clear();
            UpdatePartList();
            AddLog($"새 배치: 부품 {n}개 투입");
        }

        private void RunDetection()
        {
            if (_sceneMat == null) return;
            _currentParts = _detector.Detect(_sceneMat);
            RedrawOverlay();
            UpdatePartList();
            AddLog($"경계 검출 완료: {_currentParts.Count}개");
        }

        private void RedrawOverlay()
        {
            OverlayCanvas.Children.Clear();
            foreach (DetectedPart part in _currentParts)
                DrawBoundingBox(part);
        }

        private void DrawBoundingBox(DetectedPart part)
        {
            double theta = part.AngleDeg * Math.PI / 180.0;
            double cosT = Math.Cos(theta), sinT = Math.Sin(theta);
            double hl = part.WidthPx / 2, hw = part.HeightPx / 2;

            var poly = new Polygon
            {
                Stroke = new SolidColorBrush(Color.FromRgb(0x5D, 0xCA, 0xA5)),
                StrokeThickness = 2,
                Fill = Brushes.Transparent
            };

            double minY = double.MaxValue;
            (double, double)[] offsets = { (-hl, -hw), (hl, -hw), (hl, hw), (-hl, hw) };
            foreach ((double dx, double dy) in offsets)
            {
                double x = part.CenterPx.X + dx * cosT - dy * sinT;
                double y = part.CenterPx.Y + dx * sinT + dy * cosT;
                poly.Points.Add(new Point(x, y));
                if (y < minY) minY = y;
            }
            OverlayCanvas.Children.Add(poly);

            var cross = new System.Windows.Shapes.Path
            {
                Stroke = new SolidColorBrush(Color.FromRgb(0xF0, 0x95, 0x95)),
                StrokeThickness = 2,
                Data = Geometry.Parse(
                    $"M {part.CenterPx.X - 7},{part.CenterPx.Y} L {part.CenterPx.X + 7},{part.CenterPx.Y} " +
                    $"M {part.CenterPx.X},{part.CenterPx.Y - 7} L {part.CenterPx.X},{part.CenterPx.Y + 7}")
            };
            OverlayCanvas.Children.Add(cross);

            var label = new TextBlock
            {
                Text = $"{Math.Round(part.AngleDeg)}\u00b0",
                Foreground = Brushes.White,
                Background = new SolidColorBrush(Color.FromArgb(190, 20, 20, 18)),
                FontSize = 11,
                Padding = new Thickness(3, 1, 3, 1)
            };
            Canvas.SetLeft(label, part.CenterPx.X - 14);
            Canvas.SetTop(label, minY - 20);
            OverlayCanvas.Children.Add(label);
        }

        private void DrawMark(double x, double y, double angleDeg)
        {
            string crossGeom = $"M {x - 9},{y} L {x + 9},{y} M {x},{y - 9} L {x},{y + 9}";
            var rotate = new RotateTransform(angleDeg, x, y);

            var crossWhite = new System.Windows.Shapes.Path
            {
                Stroke = new SolidColorBrush(Color.FromArgb(230, 255, 255, 255)),
                StrokeThickness = 4,
                Data = Geometry.Parse(crossGeom),
                RenderTransform = rotate
            };
            var crossDark = new System.Windows.Shapes.Path
            {
                Stroke = new SolidColorBrush(Color.FromRgb(0x2C, 0x2C, 0x2A)),
                StrokeThickness = 2,
                Data = Geometry.Parse(crossGeom),
                RenderTransform = rotate
            };
            var ring = new Ellipse
            {
                Width = 22,
                Height = 22,
                Stroke = new SolidColorBrush(Color.FromRgb(0x53, 0x4A, 0xB7)),
                StrokeThickness = 1.5
            };
            Canvas.SetLeft(ring, x - 11);
            Canvas.SetTop(ring, y - 11);

            OverlayCanvas.Children.Add(crossWhite);
            OverlayCanvas.Children.Add(crossDark);
            OverlayCanvas.Children.Add(ring);

            PlayFlash(x, y);
        }

        private void PlayFlash(double x, double y)
        {
            var flash = new Ellipse
            {
                Width = 20,
                Height = 20,
                Stroke = new SolidColorBrush(Color.FromRgb(0x7F, 0x77, 0xDD)),
                StrokeThickness = 2,
                Opacity = 0.9,
                RenderTransformOrigin = new Point(0.5, 0.5)
            };
            var scale = new ScaleTransform(1, 1);
            flash.RenderTransform = scale;
            Canvas.SetLeft(flash, x - 10);
            Canvas.SetTop(flash, y - 10);
            OverlayCanvas.Children.Add(flash);

            var scaleAnim = new DoubleAnimation(1, 3.2, TimeSpan.FromMilliseconds(350));
            var opacityAnim = new DoubleAnimation(0.9, 0, TimeSpan.FromMilliseconds(350));
            opacityAnim.Completed += (s, e) => OverlayCanvas.Children.Remove(flash);
            scale.BeginAnimation(ScaleTransform.ScaleXProperty, scaleAnim);
            scale.BeginAnimation(ScaleTransform.ScaleYProperty, scaleAnim);
            flash.BeginAnimation(UIElement.OpacityProperty, opacityAnim);
        }

        private void FireMark(double px, double py, double angleDeg)
        {
            DrawMark(px, py, angleDeg);
            Point2D mm = _calibrator.PixelToMachine(new Point2D(px, py));
            double rotation = angleDeg + _calibrator.GetCameraToMachineRotationDeg();
            _laser.MarkAt(mm.X, mm.Y, rotation);
            _producedCount++;
            ProducedCountText.Text = _producedCount.ToString();
        }

        private string FormatMm(double px, double py)
        {
            Point2D mm = _calibrator.PixelToMachine(new Point2D(px, py));
            return $"{mm.X:F1}, {mm.Y:F1}";
        }

        private void OnLaserMarked(double x, double y, double angle)
        {
            // 실제 보드 연동 시 이 이벤트에서 가공완료 피드백/상태를 추가로 받아 처리할 수 있다.
        }

        private void UpdatePartList()
        {
            PartListBox.Items.Clear();
            for (int i = 0; i < _currentParts.Count; i++)
            {
                DetectedPart p = _currentParts[i];
                string mm = FormatMm(p.CenterPx.X, p.CenterPx.Y);
                PartListBox.Items.Add(
                    $"#{i + 1}  px({Math.Round(p.CenterPx.X)}, {Math.Round(p.CenterPx.Y)})  {Math.Round(p.AngleDeg)}\u00b0  \u2192  mm({mm})");
            }
            DetectedCountText.Text = _currentParts.Count.ToString();
        }

        private void AddLog(string text)
        {
            MarkLogListBox.Items.Insert(0, text);
            while (MarkLogListBox.Items.Count > 8)
                MarkLogListBox.Items.RemoveAt(MarkLogListBox.Items.Count - 1);
        }

        private void RegenButton_Click(object sender, RoutedEventArgs e) => Regenerate();

        private void DetectButton_Click(object sender, RoutedEventArgs e) => RunDetection();

        private async void AutoMarkButton_Click(object sender, RoutedEventArgs e)
        {
            if (_autoMarkRunning) return;

            if (_currentParts.Count == 0)
                RunDetection();

            if (_currentParts.Count == 0)
            {
                AddLog("검출된 부품이 없습니다");
                return;
            }

            _autoMarkRunning = true;
            AutoMarkButton.IsEnabled = false;

            foreach (DetectedPart part in _currentParts)
            {
                await Task.Delay(450);
                FireMark(part.CenterPx.X, part.CenterPx.Y, part.AngleDeg);
                AddLog($"자동 마킹 \u2192 mm({FormatMm(part.CenterPx.X, part.CenterPx.Y)})  {Math.Round(part.AngleDeg)}\u00b0");
            }

            AutoMarkButton.IsEnabled = true;
            _autoMarkRunning = false;
        }

        private void ManualToggleButton_Click(object sender, RoutedEventArgs e)
        {
            _manualMode = !_manualMode;
            ManualToggleButton.Content = _manualMode ? "수동 마킹: ON" : "수동 마킹: OFF";
            ModeText.Text = _manualMode ? "수동" : "자동";
            HintText.Text = _manualMode ? "카메라 화면을 클릭하면 그 위치에 가상으로 마킹됩니다." : "";
            OverlayCanvas.Cursor = _manualMode ? Cursors.Cross : Cursors.Hand;
        }

        private void OverlayCanvas_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (!_manualMode) return;
            Point pos = e.GetPosition(OverlayCanvas);
            FireMark(pos.X, pos.Y, 0);
            AddLog($"수동 마킹 \u2192 mm({FormatMm(pos.X, pos.Y)})");
        }

        private void PartCountSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (PartCountLabel != null)
                PartCountLabel.Text = ((int)e.NewValue).ToString();
        }
    }
}
