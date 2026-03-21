using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using OB.Models;
using Prism.Commands;
using Prism.Mvvm;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace OB.ViewModels
{
    public class AutoDetViewModel : BindableBase
    {
        private Bitmap? _currentImage;
        public Bitmap? CurrentImage
        {
            get => _currentImage;
            set => SetProperty(ref _currentImage, value);
        }

        private string _currentImagePath = string.Empty;
        public string CurrentImagePath
        {
            get => _currentImagePath;
            set => SetProperty(ref _currentImagePath, value);
        }

        private ObservableCollection<Annotation> _annotations = new();
        public ObservableCollection<Annotation> Annotations
        {
            get => _annotations;
            set => SetProperty(ref _annotations, value);
        }

        private Annotation? _selectedAnnotation;
        public Annotation? SelectedAnnotation
        {
            get => _selectedAnnotation;
            set => SetProperty(ref _selectedAnnotation, value);
        }

        private string _modeText = "模式：矩形";
        public string ModeText
        {
            get => _modeText;
            set => SetProperty(ref _modeText, value);
        }

        private string _statusText = "就緒";
        public string StatusText
        {
            get => _statusText;
            set => SetProperty(ref _statusText, value);
        }

        // Commands
        public DelegateCommand LoadImageCommand { get; }
        public DelegateCommand SetRectModeCommand { get; }
        public DelegateCommand SetPolygonModeCommand { get; }
        public DelegateCommand SaveAnnotationsCommand { get; }

        // Drawing state
        private enum DrawMode { None, Rectangle, Polygon }
        private DrawMode _currentDrawMode = DrawMode.Rectangle;
        private Annotation? _tempPolygon;
        private Point _rectStart;
        private bool _isDrawing;

        // Controls
        private Image? _imageControl;
        private Canvas? _canvas;

        // Scale & size
        private double _imageWidth;
        private double _imageHeight;
        private double _canvasScale = 1.0;

        public AutoDetViewModel()
        {
            LoadImageCommand = new DelegateCommand(async () => await OnLoadImageAsync());
            SetRectModeCommand = new DelegateCommand(() => SetDrawMode(DrawMode.Rectangle));
            SetPolygonModeCommand = new DelegateCommand(() => SetDrawMode(DrawMode.Polygon));
            SaveAnnotationsCommand = new DelegateCommand(async () => await OnSaveAnnotationsAsync());
        }

        public void SetControls(Image image, Canvas canvas)
        {
            _imageControl = image;
            _canvas = canvas;

            if (_imageControl != null)
            {
                _imageControl.AttachedToVisualTree += (_, _) => UpdateImageInfo();
                _imageControl.PropertyChanged += (_, e) =>
                {
                    if (e.Property == Image.SourceProperty)
                        UpdateImageInfo();
                };
            }
        }

        private void UpdateImageInfo()
        {
            if (CurrentImage == null || _imageControl == null) return;

            _imageWidth = CurrentImage.Size.Width;
            _imageHeight = CurrentImage.Size.Height;

            Dispatcher.UIThread.Post(() =>
            {
                if (_imageControl.Bounds.Width <= 0 || _imageControl.Bounds.Height <= 0) return;
                var renderWidth = _imageControl.Bounds.Width;
                var renderHeight = _imageControl.Bounds.Height;
                _canvasScale = Math.Min(renderWidth / _imageWidth, renderHeight / _imageHeight);
            }, DispatcherPriority.Loaded);
        }

        private void SetDrawMode(DrawMode mode)
        {
            _currentDrawMode = mode;
            ModeText = mode == DrawMode.Rectangle ? "模式：矩形" : "模式：多邊形";
            CancelPolygonDrawing();
        }

        private void CancelPolygonDrawing()
        {
            if (_tempPolygon != null)
            {
                ClearTempShapes();
                _tempPolygon = null;
            }
        }

        // ─── 以下方法由 code-behind 呼叫 ───

        public void OnPointerPressed(PointerPressedEventArgs e)
        {
            if (_canvas == null) return;
            var point = e.GetPosition(_canvas);

            if (_currentDrawMode == DrawMode.Rectangle)
            {
                _rectStart = point;
                _isDrawing = true;
            }
            else if (_currentDrawMode == DrawMode.Polygon)
            {
                if (_tempPolygon == null)
                {
                    _tempPolygon = new Annotation
                    {
                        Type = AnnotationType.Polygon,
                        ClassId = 0,
                        ClassName = "待分類",
                        Points = new List<Point> { point }
                    };
                    DrawPoint(point, isTemp: true);
                }
                else
                {
                    _tempPolygon.Points.Add(point);
                    RedrawTempPolygon();
                }
            }
        }

        public void OnPointerMoved(PointerEventArgs e)
        {
            if (_currentDrawMode != DrawMode.Rectangle || !_isDrawing || _canvas == null) return;

            var current = e.GetPosition(_canvas);
            DrawPreviewRectangle(_rectStart, current);
        }

        public void OnPointerReleased(PointerReleasedEventArgs e)
        {
            if (_currentDrawMode != DrawMode.Rectangle || !_isDrawing || _canvas == null) return;

            _isDrawing = false;
            var rectEnd = e.GetPosition(_canvas);
            ClearPreviewRectangle();

            var ann = new Annotation
            {
                Type = AnnotationType.BoundingBox,
                ClassId = 0,
                ClassName = "待分類",
                Points = new List<Point> { _rectStart, rectEnd }
            };

            Annotations.Add(ann);
            DrawAnnotation(ann);
            StatusText = $"已新增矩形，共 {Annotations.Count} 個標註";
        }

        public void OnPointerRightPressed(PointerPressedEventArgs e)
        {
            if (_currentDrawMode != DrawMode.Polygon || _tempPolygon == null) return;

            if (e.GetCurrentPoint(null).Properties.IsRightButtonPressed)
            {
                if (_tempPolygon.Points.Count >= 3)
                {
                    Annotations.Add(_tempPolygon);
                    DrawAnnotation(_tempPolygon);
                    StatusText = $"已新增多邊形，共 {Annotations.Count} 個標註";
                }
                else
                {
                    StatusText = "多邊形點數不足，已取消";
                }

                _tempPolygon = null;
                ClearTempShapes();
                e.Handled = true;
            }
        }

        // ─── 繪圖輔助方法 ───

        private void DrawPoint(Point p, bool isTemp)
        {
            if (_canvas == null) return;
            Dispatcher.UIThread.Post(() =>
            {
                var ellipse = new Avalonia.Controls.Shapes.Ellipse
                {
                    Width = 6,
                    Height = 6,
                    Fill = isTemp ? Avalonia.Media.Brushes.Red : Avalonia.Media.Brushes.LimeGreen,
                    Tag = isTemp ? "temp" : null
                };
                Canvas.SetLeft(ellipse, p.X - 3);
                Canvas.SetTop(ellipse, p.Y - 3);
                _canvas.Children.Add(ellipse);
            });
        }

        private void DrawLine(Point p1, Point p2, bool isTemp)
        {
            if (_canvas == null) return;
            Dispatcher.UIThread.Post(() =>
            {
                var line = new Avalonia.Controls.Shapes.Line
                {
                    StartPoint = p1,
                    EndPoint = p2,
                    Stroke = isTemp ? Avalonia.Media.Brushes.Red : Avalonia.Media.Brushes.LimeGreen,
                    StrokeThickness = 2,
                    Tag = isTemp ? "temp" : null
                };
                _canvas.Children.Add(line);
            });
        }

        private void RedrawTempPolygon()
        {
            ClearTempShapes();
            if (_tempPolygon == null || _tempPolygon.Points.Count < 2) return;

            for (int i = 0; i < _tempPolygon.Points.Count - 1; i++)
                DrawLine(_tempPolygon.Points[i], _tempPolygon.Points[i + 1], true);

            foreach (var p in _tempPolygon.Points)
                DrawPoint(p, true);
        }

        private void ClearTempShapes()
        {
            if (_canvas == null) return;
            Dispatcher.UIThread.Post(() =>
            {
                var toRemove = _canvas.Children
                    .Where(c => c.Tag is string tag && tag == "temp")
                    .ToList();

                foreach (var child in toRemove)
                    _canvas.Children.Remove(child);
            });
        }

        private void DrawPreviewRectangle(Point start, Point current)
        {
            if (_canvas == null) return;
            Dispatcher.UIThread.Post(() =>
            {
                ClearPreviewRectangle();

                var rect = new Avalonia.Controls.Shapes.Rectangle
                {
                    Stroke = Avalonia.Media.Brushes.Red,
                    StrokeThickness = 2,
                    StrokeDashArray = new Avalonia.Collections.AvaloniaList<double> { 4, 2 },
                    Tag = "preview"
                };

                double left = Math.Min(start.X, current.X);
                double top = Math.Min(start.Y, current.Y);
                double width = Math.Abs(current.X - start.X);
                double height = Math.Abs(current.Y - start.Y);

                Canvas.SetLeft(rect, left);
                Canvas.SetTop(rect, top);
                rect.Width = width;
                rect.Height = height;

                _canvas.Children.Add(rect);
            });
        }

        private void ClearPreviewRectangle()
        {
            if (_canvas == null) return;
            Dispatcher.UIThread.Post(() =>
            {
                var preview = _canvas.Children.FirstOrDefault(c => c.Tag is string tag && tag == "preview");
                if (preview != null)
                    _canvas.Children.Remove(preview);
            });
        }

        private void DrawAnnotation(Annotation ann)
        {
            if (_canvas == null) return;
            Dispatcher.UIThread.Post(() =>
            {
                if (ann.Type == AnnotationType.BoundingBox && ann.Points.Count >= 2)
                {
                    var p1 = ann.Points[0];
                    var p2 = ann.Points[1];
                    double left = Math.Min(p1.X, p2.X);
                    double top = Math.Min(p1.Y, p2.Y);
                    double width = Math.Abs(p2.X - p1.X);
                    double height = Math.Abs(p2.Y - p1.Y);

                    var rect = new Avalonia.Controls.Shapes.Rectangle
                    {
                        Stroke = Avalonia.Media.Brushes.LimeGreen,
                        StrokeThickness = 2,
                        Tag = ann
                    };

                    Canvas.SetLeft(rect, left);
                    Canvas.SetTop(rect, top);
                    rect.Width = width;
                    rect.Height = height;

                    _canvas.Children.Add(rect);
                }
                else if (ann.Type == AnnotationType.Polygon && ann.Points.Count >= 3)
                {
                    for (int i = 0; i < ann.Points.Count - 1; i++)
                        DrawLine(ann.Points[i], ann.Points[i + 1], false);

                    DrawLine(ann.Points[^1], ann.Points[0], false);

                    foreach (var p in ann.Points)
                        DrawPoint(p, false);
                }
            });
        }

        // ─── 載入與儲存 ───

        private async Task OnLoadImageAsync()
        {
            var dlg = new OpenFileDialog
            {
                AllowMultiple = false,
                Filters = { new FileDialogFilter { Name = "Images", Extensions = { "jpg", "jpeg", "png", "bmp" } } }
            };

            var result = await dlg.ShowAsync(null);
            if (result == null || result.Length == 0) return;

            var path = result[0];
            try
            {
                CurrentImage = new Bitmap(path);
                CurrentImagePath = path;
                Annotations.Clear();

                if (_canvas != null)
                    _canvas.Children.Clear();

                StatusText = $"已載入：{Path.GetFileName(path)}";
            }
            catch (Exception ex)
            {
                StatusText = $"載入失敗：{ex.Message}";
            }
        }

        private async Task OnSaveAnnotationsAsync()
        {
            if (CurrentImage == null || string.IsNullOrEmpty(CurrentImagePath)) return;

            var dlg = new SaveFileDialog
            {
                Filters = { new FileDialogFilter { Name = "YOLO txt", Extensions = { "txt" } } },
                InitialFileName = Path.GetFileNameWithoutExtension(CurrentImagePath) + ".txt"
            };

            var path = await dlg.ShowAsync(null);
            if (string.IsNullOrEmpty(path)) return;

            try
            {
                using var sw = new StreamWriter(path);

                foreach (var ann in Annotations)
                {
                    if (ann.Type == AnnotationType.BoundingBox && ann.Points.Count >= 2)
                    {
                        var box = ann.GetBoundingBox();
                        double x_center = (box.X + box.Width / 2) / _canvasScale / _imageWidth;
                        double y_center = (box.Y + box.Height / 2) / _canvasScale / _imageHeight;
                        double w = box.Width / _canvasScale / _imageWidth;
                        double h = box.Height / _canvasScale / _imageHeight;

                        sw.WriteLine($"{ann.ClassId} {x_center:F6} {y_center:F6} {w:F6} {h:F6}");
                    }
                    else if (ann.Type == AnnotationType.Polygon && ann.Points.Count >= 3)
                    {
                        sw.Write(ann.ClassId);
                        foreach (var p in ann.Points)
                        {
                            double normX = p.X / _canvasScale / _imageWidth;
                            double normY = p.Y / _canvasScale / _imageHeight;
                            sw.Write($" {normX:F6} {normY:F6}");
                        }
                        sw.WriteLine();
                    }
                }

                StatusText = $"已儲存至：{Path.GetFileName(path)}";
            }
            catch (Exception ex)
            {
                StatusText = $"儲存失敗：{ex.Message}";
            }
        }
    }
}