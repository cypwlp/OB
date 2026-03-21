using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Docnet.Core;
using Docnet.Core.Models;
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
        private readonly string UploadImagesPath = @"\\10.241.48.21\Users\OBAnotaion\images";
        private readonly string SaveAnnotationsPath = @"\\10.241.48.21\Users\OBAnotaion\labels";

        // ──────────────────────────────────────────────────────────────
        // 原本的多圖片列表 → 改名為 ExpectedImagePaths (預期圖片清單)
        // ──────────────────────────────────────────────────────────────
        private ObservableCollection<string> _expectedImagePaths = new ObservableCollection<string>();
        public ObservableCollection<string> ExpectedImagePaths
        {
            get => _expectedImagePaths;
            set => SetProperty(ref _expectedImagePaths, value);
        }

        // 新增：已經真正產生完成的圖片路徑集合
        private readonly HashSet<string> _completedPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        private int _currentImageIndex = -1;
        public int CurrentImageIndex
        {
            get => _currentImageIndex;
            set
            {
                SetProperty(ref _currentImageIndex, value);
                if (value >= 0)
                {
                    LoadCurrentImage();
                }
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

        // 新增：目前圖片載入狀態提示
        private string _loadStatus = "就緒";
        public string LoadStatus
        {
            get => _loadStatus;
            set => SetProperty(ref _loadStatus, value);
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
            set => SetProperty(ref _selectedAnnotation, value);
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
                }
            }
        }

        // Commands
        public DelegateCommand SetRectModeCommand { get; }
        public DelegateCommand SetPolygonModeCommand { get; }
        public DelegateCommand PrevImageCommand { get; }
        public DelegateCommand NextImageCommand { get; }
        public DelegateCommand SaveAnnotationsCommand { get; }

        // 繪圖狀態
        private enum DrawMode { None, Rectangle, Polygon }
        private DrawMode _currentDrawMode = DrawMode.Rectangle;
        private Annotation? _tempPolygon;
        private Point _rectStart;
        private bool _isDrawing;

        // 控制項參考
        private Image? _imageControl;
        private Canvas? _canvas;

        // 縮放資訊
        private double _imageWidth;
        private double _imageHeight;
        private double _canvasScale = 1.0;

        public AutoDetViewModel()
        {
            SetRectModeCommand = new DelegateCommand(() => IsPolygonMode = false);
            SetPolygonModeCommand = new DelegateCommand(() => IsPolygonMode = true);

            PrevImageCommand = new DelegateCommand(() =>
            {
                if (CurrentImageIndex > 0) CurrentImageIndex--;
            });

            NextImageCommand = new DelegateCommand(() =>
            {
                if (CurrentImageIndex < ExpectedImagePaths.Count - 1) CurrentImageIndex++;
            });

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

        // ─── 畫布事件處理 ───
        public void OnPointerPressed(PointerPressedEventArgs e)
        {
            if (_canvas == null) return;
            var point = e.GetPosition(_canvas);
            var currentPoint = e.GetCurrentPoint(_canvas);
            if (currentPoint.Properties.IsLeftButtonPressed)
            {
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
                        DrawPoint(point, true);
                    }
                    else
                    {
                        _tempPolygon.Points.Add(point);
                        RedrawTempPolygon();
                    }
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

        public void OnPointerPressedRight(PointerPressedEventArgs e)
        {
            if (_currentDrawMode != DrawMode.Polygon || _tempPolygon == null) return;
            if (e.GetCurrentPoint(_canvas).Properties.IsRightButtonPressed)
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

        // ─── 繪圖輔助方法 ───（你原本就有的，保留簽名）
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

        // ─── PDF 轉圖核心 ───
        public async Task ProcessPdfFolderAsync(string folderPath)
        {
            try
            {
                var allFiles = Directory.GetFiles(folderPath);
                var pdfFiles = allFiles.Where(f => Path.GetExtension(f).Equals(".pdf", StringComparison.OrdinalIgnoreCase)).ToList();
                if (pdfFiles.Count == 0)
                {
                    StatusText = "資料夾內沒有找到 PDF 檔案";
                    return;
                }

                // 清空舊資料
                ExpectedImagePaths.Clear();
                _completedPaths.Clear();
                Annotations.Clear();
                CurrentImage = null;
                CurrentImagePath = string.Empty;
                CurrentImageIndex = -1;
                LoadStatus = "準備轉換...";
                StatusText = "正在產生預期頁面清單...";

                var tempExpected = new List<string>();

                // 先只讀頁數，產生所有預期路徑
                foreach (var pdf in pdfFiles)
                {
                    byte[] pdfBytes = await File.ReadAllBytesAsync(pdf);
                    using var docLib = DocLib.Instance;
                    using var docReader = docLib.GetDocReader(pdfBytes, new PageDimensions(1.0));
                    var pdfName = Path.GetFileNameWithoutExtension(pdf);

                    for (int i = 0; i < docReader.GetPageCount(); i++)
                    {
                        string expectedPath = Path.Combine(UploadImagesPath, $"{pdfName}_p{i + 1:D3}.jpg");
                        tempExpected.Add(expectedPath);
                    }
                }

                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    foreach (var p in tempExpected)
                    {
                        ExpectedImagePaths.Add(p);
                    }
                    StatusText = $"預計產生 {ExpectedImagePaths.Count} 張圖片，開始轉換...";
                    LoadStatus = "轉換中...";
                });

                // 真正開始轉換（背景執行）
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
                            byte[] raw = pageReader.GetImage(); // BGRA raw bytes

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

                                string outPath = Path.Combine(UploadImagesPath, $"{pdfName}_p{i + 1:D3}.jpg");
                                Directory.CreateDirectory(UploadImagesPath);
                                await File.WriteAllBytesAsync(outPath, encoded.ToArray());

                                // 通知完成
                                string completed = outPath;
                                Dispatcher.UIThread.Post(() =>
                                {
                                    _completedPaths.Add(completed);
                                    processed++;
                                    StatusText = $"已完成 {processed}/{ExpectedImagePaths.Count} 頁";

                                    // 如果使用者目前正在看這一頁 → 立即載入
                                    if (CurrentImageIndex >= 0 &&
                                        CurrentImageIndex < ExpectedImagePaths.Count &&
                                        string.Equals(ExpectedImagePaths[CurrentImageIndex], completed, StringComparison.OrdinalIgnoreCase))
                                    {
                                        LoadCurrentImage();
                                    }
                                });
                            }
                            finally
                            {
                                handle.Free();
                            }
                        }
                    }

                    await Dispatcher.UIThread.InvokeAsync(() =>
                    {
                        StatusText = $"轉換全部完成，共 {processed} 張圖片";
                        LoadStatus = "全部就緒";
                    });
                });
            }
            catch (Exception ex)
            {
                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    StatusText = $"處理失敗：{ex.Message}";
                    LoadStatus = "錯誤";
                });
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
                LoadStatus = "此頁還在轉換中，請稍候...";
                StatusText = $"頁面 {CurrentImageIndex + 1}/{ExpectedImagePaths.Count} 載入中...";
                return;
            }

            try
            {
                CurrentImage = new Bitmap(CurrentImagePath);
                Annotations.Clear();
                if (_canvas != null) _canvas.Children.Clear();
                LoadStatus = "圖片已載入";
                StatusText = $"目前：{Path.GetFileName(CurrentImagePath)} ({CurrentImageIndex + 1}/{ExpectedImagePaths.Count})";
                UpdateImageInfo();
            }
            catch (Exception ex)
            {
                LoadStatus = "載入失敗";
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
            string txtPath = Path.Combine(SaveAnnotationsPath, name + ".txt");

            try
            {
                Directory.CreateDirectory(SaveAnnotationsPath);
                using var sw = new StreamWriter(txtPath);
                foreach (var ann in Annotations)
                {
                    if (ann.Type == AnnotationType.BoundingBox && ann.Points.Count >= 2)
                    {
                        var box = ann.GetBoundingBox(); // 請確保 Annotation 有此方法
                        double xc = (box.X + box.Width / 2) / _canvasScale / _imageWidth;
                        double yc = (box.Y + box.Height / 2) / _canvasScale / _imageHeight;
                        double w = box.Width / _canvasScale / _imageWidth;
                        double h = box.Height / _canvasScale / _imageHeight;
                        sw.WriteLine($"{ann.ClassId} {xc:F6} {yc:F6} {w:F6} {h:F6}");
                    }
                    else if (ann.Type == AnnotationType.Polygon && ann.Points.Count >= 3)
                    {
                        sw.Write(ann.ClassId);
                        foreach (var p in ann.Points)
                        {
                            double nx = p.X / _canvasScale / _imageWidth;
                            double ny = p.Y / _canvasScale / _imageHeight;
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