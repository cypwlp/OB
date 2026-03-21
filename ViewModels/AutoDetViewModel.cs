using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.PanAndZoom;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Docnet.Core;
using Docnet.Core.Models;
using Microsoft.Extensions.Configuration;
using OB.Models;
using Prism.Commands;
using Prism.Mvvm;
using SkiaSharp;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace OB.ViewModels
{
    public class AutoDetViewModel : BindableBase
    {
        private string _uploadImagesPath = string.Empty;
        private string _saveAnnotationsPath = string.Empty;

        private List<ClassInfo> _classes = new List<ClassInfo>();
        public List<ClassInfo> Classes => _classes;
        private ClassInfo _selectedClass;
        public ClassInfo SelectedClass
        {
            get => _selectedClass;
            set => SetProperty(ref _selectedClass, value);
        }

        private int _polygonPointCount;
        public int PolygonPointCount
        {
            get => _polygonPointCount;
            set => SetProperty(ref _polygonPointCount, value);
        }

        private ObservableCollection<string> _expectedImagePaths = new ObservableCollection<string>();
        public ObservableCollection<string> ExpectedImagePaths
        {
            get => _expectedImagePaths;
            set => SetProperty(ref _expectedImagePaths, value);
        }

        private readonly HashSet<string> _completedPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private int _currentImageIndex = -1;
        public int CurrentImageIndex
        {
            get => _currentImageIndex;
            set
            {
                SetProperty(ref _currentImageIndex, value);
                if (value >= 0) LoadCurrentImage();
            }
        }

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

        private string _statusText = "就緒";
        public string StatusText
        {
            get => _statusText;
            set => SetProperty(ref _statusText, value);
        }

        private ObservableCollection<Annotation> _annotations = new ObservableCollection<Annotation>();
        public ObservableCollection<Annotation> Annotations
        {
            get => _annotations;
            set => SetProperty(ref _annotations, value);
        }

        private Annotation? _selectedAnnotation;
        public Annotation? SelectedAnnotation
        {
            get => _selectedAnnotation;
            set
            {
                if (SetProperty(ref _selectedAnnotation, value))
                    RedrawAllAnnotations();
            }
        }

        private string _modeText = "模式：矩形";
        public string ModeText
        {
            get => _modeText;
            set => SetProperty(ref _modeText, value);
        }

        private bool _isPolygonMode;
        public bool IsPolygonMode
        {
            get => _isPolygonMode;
            set
            {
                if (SetProperty(ref _isPolygonMode, value))
                {
                    SetDrawMode(value ? DrawMode.Polygon : DrawMode.Rectangle);
                    if (!value) CancelPolygonDrawing();
                }
            }
        }

        private double _zoomLevel = 1.0;
        public double ZoomLevel
        {
            get => _zoomLevel;
            set => SetProperty(ref _zoomLevel, value);
        }

        public DelegateCommand SetRectModeCommand { get; }
        public DelegateCommand SetPolygonModeCommand { get; }
        public DelegateCommand PrevImageCommand { get; }
        public DelegateCommand NextImageCommand { get; }
        public DelegateCommand SaveAnnotationsCommand { get; }
        public DelegateCommand ResetZoomCommand { get; }
        public DelegateCommand<Annotation> DeleteAnnotationCommand { get; }
        public DelegateCommand CancelPolygonCommand { get; }

        private enum DrawMode { None, Rectangle, Polygon }
        private DrawMode _currentDrawMode = DrawMode.Rectangle;
        private Annotation? _tempPolygon;
        private Point _rectStart;
        private bool _isDrawing;

        private Image? _imageControl;
        private Canvas? _canvas;
        private ZoomBorder? _zoomBorder;

        private double _imageWidth;
        private double _imageHeight;

        public AutoDetViewModel()
        {
            LoadConfiguration();
            InitializeClasses();
            SetRectModeCommand = new DelegateCommand(() => IsPolygonMode = false);
            SetPolygonModeCommand = new DelegateCommand(() => IsPolygonMode = true);
            PrevImageCommand = new DelegateCommand(() => { if (CurrentImageIndex > 0) CurrentImageIndex--; });
            NextImageCommand = new DelegateCommand(() => { if (CurrentImageIndex < ExpectedImagePaths.Count - 1) CurrentImageIndex++; });
            SaveAnnotationsCommand = new DelegateCommand(async () => await OnSaveAnnotationsAsync());
            ResetZoomCommand = new DelegateCommand(ResetZoom);
            DeleteAnnotationCommand = new DelegateCommand<Annotation>(DeleteAnnotation);
            CancelPolygonCommand = new DelegateCommand(CancelPolygonDrawing);
        }

        private void LoadConfiguration()
        { /* 你的原本程式碼不變 */
            try
            {
                var config = new ConfigurationBuilder()
                    .SetBasePath(AppContext.BaseDirectory)
                    .AddJsonFile("appsettings.json", optional: true, reloadOnChange: false)
                    .Build();
                _uploadImagesPath = config["ImageFolder"] ?? @"\\10.241.48.21\Users\OBAnotaion\images";
                _saveAnnotationsPath = config["LabelFolder"] ?? @"\\10.241.48.21\Users\OBAnotaion\labels";
                Directory.CreateDirectory(_uploadImagesPath);
                Directory.CreateDirectory(_saveAnnotationsPath);
            }
            catch
            {
                _uploadImagesPath = @"\\10.241.48.21\Users\OBAnotaion\images";
                _saveAnnotationsPath = @"\\10.241.48.21\Users\OBAnotaion\labels";
            }
        }

        private void InitializeClasses()
        {
            _classes = new List<ClassInfo>
            {
                new ClassInfo { Id = 0, Name = "待分類" },
                new ClassInfo { Id = 1, Name = "目標A" },
                new ClassInfo { Id = 2, Name = "目標B" },
            };
            _selectedClass = _classes[0];
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
                    if (e.Property == Image.SourceProperty) UpdateImageInfo();
                };
            }
        }

        public void SetZoomBorder(ZoomBorder zoomBorder)
        {
            _zoomBorder = zoomBorder;
            if (_zoomBorder != null)
            {
                _zoomBorder.MatrixChanged += (s, e) => ZoomLevel = _zoomBorder.Matrix.M11;
            }
        }

        private void ResetZoom()
        {
            _zoomBorder?.ResetMatrix();
            _zoomBorder?.Uniform();
        }

        private void UpdateImageInfo()
        {
            if (CurrentImage == null || _zoomBorder == null) return;
            _imageWidth = CurrentImage.Size.Width;
            _imageHeight = CurrentImage.Size.Height;
            _zoomBorder.LayoutUpdated -= OnFitAfterLayout;
            _zoomBorder.LayoutUpdated += OnFitAfterLayout;
        }

        private void OnFitAfterLayout(object? sender, EventArgs e)
        {
            if (_zoomBorder == null) return;
            _zoomBorder.LayoutUpdated -= OnFitAfterLayout;
            FitImageToView();
        }

        private void FitImageToView()
        {
            if (_zoomBorder == null) return;
            _zoomBorder.Uniform();
        }

        // 座標轉換（不變）
        private Point ToImageCoordinates(Point canvasPoint)
        {
            if (_zoomBorder == null) return canvasPoint;
            var matrix = _zoomBorder.Matrix;
            if (!matrix.HasInverse) return canvasPoint;
            var inverse = matrix.Invert();
            return inverse.Transform(canvasPoint);
        }

        private Point ToCanvasCoordinates(Point imagePoint)
        {
            if (_zoomBorder == null) return imagePoint;
            return _zoomBorder.Matrix.Transform(imagePoint);
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
                PolygonPointCount = 0;
                StatusText = "已取消多邊形繪製";
            }
        }

        // 下面所有 OnPointerPressed / Moved / Released / Right、AddAnnotation、Delete、Redraw、DrawAnnotation、DrawPoint、DrawLine、RedrawTempPolygon、ClearTempShapes、DrawPreviewRectangle、ClearPreviewRectangle 全部保持你原本的程式碼（我已完整包含）

        public void OnPointerPressed(PointerPressedEventArgs e)
        { /* 你原本程式碼 */
            if (_canvas == null) return;
            var position = e.GetPosition(_canvas);
            var imagePos = ToImageCoordinates(position);
            var currentPoint = e.GetCurrentPoint(_canvas);
            if (currentPoint.Properties.IsLeftButtonPressed)
            {
                if (_currentDrawMode == DrawMode.Rectangle)
                {
                    _rectStart = imagePos;
                    _isDrawing = true;
                }
                else if (_currentDrawMode == DrawMode.Polygon)
                {
                    if (_tempPolygon == null)
                    {
                        _tempPolygon = new Annotation
                        {
                            Type = AnnotationType.Polygon,
                            ClassId = SelectedClass.Id,
                            ClassName = SelectedClass.Name,
                            Points = new List<Point> { imagePos }
                        };
                        DrawPoint(imagePos, true);
                        PolygonPointCount = 1;
                        StatusText = "多邊形繪製中，左鍵加點，右鍵閉合";
                    }
                    else
                    {
                        _tempPolygon.Points.Add(imagePos);
                        RedrawTempPolygon();
                        PolygonPointCount = _tempPolygon.Points.Count;
                    }
                }
            }
        }

        public void OnPointerMoved(PointerEventArgs e)
        { /* 你原本程式碼 */
            if (_currentDrawMode != DrawMode.Rectangle || !_isDrawing || _canvas == null) return;
            var current = ToImageCoordinates(e.GetPosition(_canvas));
            DrawPreviewRectangle(_rectStart, current);
        }

        public void OnPointerReleased(PointerReleasedEventArgs e)
        { /* 你原本程式碼 */
            if (_currentDrawMode != DrawMode.Rectangle || !_isDrawing || _canvas == null) return;
            _isDrawing = false;
            var rectEnd = ToImageCoordinates(e.GetPosition(_canvas));
            ClearPreviewRectangle();
            var ann = new Annotation
            {
                Type = AnnotationType.BoundingBox,
                ClassId = SelectedClass.Id,
                ClassName = SelectedClass.Name,
                Points = new List<Point> { _rectStart, rectEnd }
            };
            AddAnnotation(ann);
            StatusText = $"已新增矩形，共 {Annotations.Count} 個標註";
        }

        public void OnPointerPressedRight(PointerPressedEventArgs e)
        { /* 你原本程式碼 */
            if (_currentDrawMode != DrawMode.Polygon || _tempPolygon == null) return;
            var point = e.GetCurrentPoint(_canvas);
            if (point.Properties.IsRightButtonPressed)
            {
                if (_tempPolygon.Points.Count >= 3)
                {
                    _tempPolygon.ClassId = SelectedClass.Id;
                    _tempPolygon.ClassName = SelectedClass.Name;
                    AddAnnotation(_tempPolygon);
                    StatusText = $"已新增多邊形，共 {Annotations.Count} 個標註";
                }
                else
                {
                    StatusText = "多邊形點數不足（至少3點），已取消";
                }
                _tempPolygon = null;
                ClearTempShapes();
                PolygonPointCount = 0;
                e.Handled = true;
            }
        }

        private void AddAnnotation(Annotation ann) { Annotations.Add(ann); RedrawAllAnnotations(); }
        private void DeleteAnnotation(Annotation? ann) { if (ann != null) { Annotations.Remove(ann); RedrawAllAnnotations(); StatusText = "已刪除標註"; } }

        public void RedrawAllAnnotations()
        {
            Dispatcher.UIThread.Post(() =>
            {
                if (_canvas == null) return;
                _canvas.Children.Clear();
                foreach (var ann in Annotations) DrawAnnotation(ann);
            });
        }

        private void DrawAnnotation(Annotation ann)
        { /* 你原本程式碼 */
            if (_canvas == null) return;
            var isSelected = ann == SelectedAnnotation;
            var strokeBrush = isSelected ? Brushes.Yellow : Brushes.LimeGreen;
            if (ann.Type == AnnotationType.BoundingBox && ann.Points.Count >= 2)
            {
                var p1 = ToCanvasCoordinates(ann.Points[0]);
                var p2 = ToCanvasCoordinates(ann.Points[1]);
                double left = Math.Min(p1.X, p2.X);
                double top = Math.Min(p1.Y, p2.Y);
                var rect = new Avalonia.Controls.Shapes.Rectangle
                {
                    Stroke = strokeBrush,
                    StrokeThickness = 2,
                    Tag = ann
                };
                Canvas.SetLeft(rect, left);
                Canvas.SetTop(rect, top);
                rect.Width = Math.Abs(p2.X - p1.X);
                rect.Height = Math.Abs(p2.Y - p1.Y);
                _canvas.Children.Add(rect);
            }
            else if (ann.Type == AnnotationType.Polygon && ann.Points.Count >= 3)
            {
                for (int i = 0; i < ann.Points.Count - 1; i++)
                    DrawLine(ann.Points[i], ann.Points[i + 1], false, strokeBrush);
                DrawLine(ann.Points[^1], ann.Points[0], false, strokeBrush);
                foreach (var p in ann.Points) DrawPoint(p, false, strokeBrush);
            }
        }

        private void DrawPoint(Point imagePoint, bool isTemp, IBrush? customBrush = null)
        { /* 你原本程式碼 */
            if (_canvas == null) return;
            var canvasPoint = ToCanvasCoordinates(imagePoint);
            Dispatcher.UIThread.Post(() =>
            {
                var fill = customBrush ?? (isTemp ? Brushes.Red : Brushes.LimeGreen);
                var ellipse = new Avalonia.Controls.Shapes.Ellipse
                {
                    Width = 6,
                    Height = 6,
                    Fill = fill,
                    Tag = isTemp ? "temp" : null
                };
                Canvas.SetLeft(ellipse, canvasPoint.X - 3);
                Canvas.SetTop(ellipse, canvasPoint.Y - 3);
                _canvas.Children.Add(ellipse);
            });
        }

        private void DrawLine(Point p1, Point p2, bool isTemp, IBrush? customBrush = null)
        { /* 你原本程式碼 */
            if (_canvas == null) return;
            var canvasP1 = ToCanvasCoordinates(p1);
            var canvasP2 = ToCanvasCoordinates(p2);
            Dispatcher.UIThread.Post(() =>
            {
                var stroke = customBrush ?? (isTemp ? Brushes.Red : Brushes.LimeGreen);
                var line = new Avalonia.Controls.Shapes.Line
                {
                    StartPoint = canvasP1,
                    EndPoint = canvasP2,
                    Stroke = stroke,
                    StrokeThickness = 2,
                    Tag = isTemp ? "temp" : null
                };
                _canvas.Children.Add(line);
            });
        }

        private void RedrawTempPolygon()
        {
            ClearTempShapes(); if (_tempPolygon == null || _tempPolygon.Points.Count < 2) return;
            for (int i = 0; i < _tempPolygon.Points.Count - 1; i++)
                DrawLine(_tempPolygon.Points[i], _tempPolygon.Points[i + 1], true);
            foreach (var p in _tempPolygon.Points) DrawPoint(p, true);
        }

        private void ClearTempShapes()
        {
            if (_canvas == null) return;
            Dispatcher.UIThread.Post(() =>
            {
                var toRemove = _canvas.Children.Where(c => c.Tag is string tag && tag == "temp").ToList();
                foreach (var child in toRemove) _canvas.Children.Remove(child);
            });
        }

        private void DrawPreviewRectangle(Point start, Point current)
        { /* 你原本程式碼 */
            if (_canvas == null) return;
            Dispatcher.UIThread.Post(() =>
            {
                ClearPreviewRectangle();
                var rect = new Avalonia.Controls.Shapes.Rectangle
                {
                    Stroke = Brushes.Red,
                    StrokeThickness = 2,
                    StrokeDashArray = new Avalonia.Collections.AvaloniaList<double> { 4, 2 },
                    Tag = "preview"
                };
                var canvasStart = ToCanvasCoordinates(start);
                var canvasCurrent = ToCanvasCoordinates(current);
                double left = Math.Min(canvasStart.X, canvasCurrent.X);
                double top = Math.Min(canvasStart.Y, canvasCurrent.Y);
                rect.Width = Math.Abs(canvasCurrent.X - canvasStart.X);
                rect.Height = Math.Abs(canvasCurrent.Y - canvasStart.Y);
                Canvas.SetLeft(rect, left);
                Canvas.SetTop(rect, top);
                _canvas.Children.Add(rect);
            });
        }

        private void ClearPreviewRectangle()
        {
            if (_canvas == null) return;
            Dispatcher.UIThread.Post(() =>
            {
                var preview = _canvas.Children.FirstOrDefault(c => c.Tag is string tag && tag == "preview");
                if (preview != null) _canvas.Children.Remove(preview);
            });
        }

        public async Task ProcessPdfFolderAsync(string folderPath)
        { /* 你原本完整程式碼 */
            // （這裡貼你原本的 ProcessPdfFolderAsync 全部內容，為了節省篇幅我省略，但請保持你原本的）
            try
            {
                var allFiles = Directory.GetFiles(folderPath);
                var pdfFiles = allFiles.Where(f => Path.GetExtension(f).Equals(".pdf", StringComparison.OrdinalIgnoreCase)).ToList();
                if (pdfFiles.Count == 0)
                {
                    StatusText = "資料夾內沒有找到 PDF 檔案";
                    return;
                }
                ExpectedImagePaths.Clear();
                _completedPaths.Clear();
                Annotations.Clear();
                CurrentImage = null;
                CurrentImagePath = string.Empty;
                CurrentImageIndex = -1;
                StatusText = "正在產生預期頁面清單...";
                var tempExpected = new List<string>();
                foreach (var pdf in pdfFiles)
                {
                    byte[] pdfBytes = await File.ReadAllBytesAsync(pdf);
                    using var docLib = DocLib.Instance;
                    using var docReader = docLib.GetDocReader(pdfBytes, new PageDimensions(1.0));
                    var pdfName = Path.GetFileNameWithoutExtension(pdf);
                    for (int i = 0; i < docReader.GetPageCount(); i++)
                    {
                        string expectedPath = Path.Combine(_uploadImagesPath, $"{pdfName}_p{i + 1:D3}.jpg");
                        tempExpected.Add(expectedPath);
                    }
                }
                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    foreach (var p in tempExpected) ExpectedImagePaths.Add(p);
                    StatusText = $"預計產生 {ExpectedImagePaths.Count} 張圖片，開始轉換...";
                });
                _ = Task.Run(async () =>
                {
                    int processed = 0;
                    foreach (var pdf in pdfFiles)
                    {
                        var pdfName = Path.GetFileNameWithoutExtension(pdf);
                        const double dpi = 300.0;
                        const double scale = dpi / 72.0;
                        byte[] pdfBytes = await File.ReadAllBytesAsync(pdf);
                        using var docLib = DocLib.Instance;
                        using var docReader = docLib.GetDocReader(pdfBytes, new PageDimensions(scale));
                        for (int i = 0; i < docReader.GetPageCount(); i++)
                        {
                            using var pageReader = docReader.GetPageReader(i);
                            int w = pageReader.GetPageWidth();
                            int h = pageReader.GetPageHeight();
                            byte[] raw = pageReader.GetImage();
                            var info = new SKImageInfo(w, h, SKColorType.Bgra8888, SKAlphaType.Premul);
                            using var bitmap = new SKBitmap();
                            var handle = GCHandle.Alloc(raw, GCHandleType.Pinned);
                            try
                            {
                                bool success = bitmap.InstallPixels(info, handle.AddrOfPinnedObject(), info.RowBytes);
                                if (!success) continue;
                                using var skImage = SKImage.FromBitmap(bitmap);
                                using var encoded = skImage.Encode(SKEncodedImageFormat.Jpeg, 92);
                                if (encoded == null) continue;
                                string outPath = Path.Combine(_uploadImagesPath, $"{pdfName}_p{i + 1:D3}.jpg");
                                Directory.CreateDirectory(_uploadImagesPath);
                                await File.WriteAllBytesAsync(outPath, encoded.ToArray());
                                string completed = outPath;
                                Dispatcher.UIThread.Post(() =>
                                {
                                    _completedPaths.Add(completed);
                                    processed++;
                                    StatusText = $"已完成 {processed}/{ExpectedImagePaths.Count} 頁";
                                    if (CurrentImageIndex >= 0 && CurrentImageIndex < ExpectedImagePaths.Count &&
                                        string.Equals(ExpectedImagePaths[CurrentImageIndex], completed, StringComparison.OrdinalIgnoreCase))
                                    {
                                        LoadCurrentImage();
                                    }
                                });
                            }
                            finally { handle.Free(); }
                        }
                    }
                    await Dispatcher.UIThread.InvokeAsync(() =>
                    {
                        StatusText = $"轉換全部完成，共 {processed} 張圖片";
                    });
                });
            }
            catch (Exception ex)
            {
                await Dispatcher.UIThread.InvokeAsync(() => StatusText = $"處理失敗：{ex.Message}");
            }
        }

        private void LoadCurrentImage()
        {
            if (CurrentImageIndex < 0 || CurrentImageIndex >= ExpectedImagePaths.Count) return;
            CurrentImagePath = ExpectedImagePaths[CurrentImageIndex];
            if (!_completedPaths.Contains(CurrentImagePath))
            {
                CurrentImage = null;
                Annotations.Clear();
                if (_canvas != null) _canvas.Children.Clear();
                StatusText = $"頁面 {CurrentImageIndex + 1}/{ExpectedImagePaths.Count} 載入中...";
                return;
            }
            try
            {
                CurrentImage = new Bitmap(CurrentImagePath);
                Annotations.Clear();
                if (_canvas != null) _canvas.Children.Clear();
                StatusText = $"目前：{Path.GetFileName(CurrentImagePath)} ({CurrentImageIndex + 1}/{ExpectedImagePaths.Count})";
                UpdateImageInfo();

                // ★★★ 關鍵修正：強制在下一個渲染週期執行 Uniform，讓大圖自動完整填滿視窗 ★★★
                if (_zoomBorder != null)
                {
                    Dispatcher.UIThread.Post(FitImageToView, DispatcherPriority.Render);
                }
            }
            catch (Exception ex)
            {
                StatusText = $"載入失敗：{ex.Message}";
            }
        }

        private async Task OnSaveAnnotationsAsync()
        {
            if (string.IsNullOrEmpty(CurrentImagePath) || Annotations.Count == 0)
            {
                StatusText = "無圖片或無標註";
                return;
            }
            string name = Path.GetFileNameWithoutExtension(CurrentImagePath);
            string txtPath = Path.Combine(_saveAnnotationsPath, name + ".txt");
            try
            {
                Directory.CreateDirectory(_saveAnnotationsPath);
                using var sw = new StreamWriter(txtPath);
                foreach (var ann in Annotations)
                {
                    if (ann.Type == AnnotationType.BoundingBox && ann.Points.Count >= 2)
                    {
                        var p1 = ann.Points[0];
                        var p2 = ann.Points[1];
                        double xMin = Math.Min(p1.X, p2.X);
                        double yMin = Math.Min(p1.Y, p2.Y);
                        double width = Math.Abs(p2.X - p1.X);
                        double height = Math.Abs(p2.Y - p1.Y);
                        double xc = (xMin + width / 2) / _imageWidth;
                        double yc = (yMin + height / 2) / _imageHeight;
                        double w = width / _imageWidth;
                        double h = height / _imageHeight;
                        sw.WriteLine($"{ann.ClassId} {xc:F6} {yc:F6} {w:F6} {h:F6}");
                    }
                    else if (ann.Type == AnnotationType.Polygon && ann.Points.Count >= 3)
                    {
                        sw.Write(ann.ClassId);
                        foreach (var p in ann.Points)
                        {
                            double nx = p.X / _imageWidth;
                            double ny = p.Y / _imageHeight;
                            sw.Write($" {nx:F6} {ny:F6}");
                        }
                        sw.WriteLine();
                    }
                }
                StatusText = $"已儲存標註：{Path.GetFileName(txtPath)}";
            }
            catch (Exception ex)
            {
                StatusText = $"儲存失敗：{ex.Message}";
            }
        }
    }
}