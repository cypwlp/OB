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

        // 多圖片列表
        private ObservableCollection<string> _imagePaths = new();
        public ObservableCollection<string> ImagePaths
        {
            get => _imagePaths;
            set => SetProperty(ref _imagePaths, value);
        }

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

        // 用來控制 ToggleButton 狀態
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
                if (CurrentImageIndex < ImagePaths.Count - 1) CurrentImageIndex++;
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

        // ─── 繪圖輔助方法 ───（保持原樣，這裡省略以節省篇幅，你可以直接從你原本的程式碼貼上）
        private void DrawPoint(Point p, bool isTemp) { /* ... 原內容 ... */ }
        private void DrawLine(Point p1, Point p2, bool isTemp) { /* ... 原內容 ... */ }
        private void RedrawTempPolygon() { /* ... 原內容 ... */ }
        private void ClearTempShapes() { /* ... 原內容 ... */ }
        private void DrawPreviewRectangle(Point start, Point current) { /* ... 原內容 ... */ }
        private void ClearPreviewRectangle() { /* ... 原內容 ... */ }
        private void DrawAnnotation(Annotation ann) { /* ... 原內容 ... */ }

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

                StatusText = $"正在處理 {pdfFiles.Count} 個 PDF ...";

                var generated = new List<string>();

                foreach (var pdf in pdfFiles)
                {
                    var jpgs = await ConvertPdfToJpgs300DpiAsync(pdf);
                    generated.AddRange(jpgs);
                }

                ImagePaths = new ObservableCollection<string>(generated.OrderBy(x => x));
                if (ImagePaths.Count > 0)
                {
                    CurrentImageIndex = 0;
                    StatusText = $"處理完成，共產生 {ImagePaths.Count} 張圖片";
                }
            }
            catch (Exception ex)
            {
                StatusText = $"處理失敗：{ex.Message}";
            }
        }

        private async Task<List<string>> ConvertPdfToJpgs300DpiAsync(string pdfPath)
        {
            var pdfName = Path.GetFileNameWithoutExtension(pdfPath);
            var generated = new List<string>();

            byte[] pdfBytes = await File.ReadAllBytesAsync(pdfPath);
            const double dpi = 300.0;
            const double scale = dpi / 72.0;

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
                    // 關鍵：使用 IntPtr + rowBytes
                    bool success = bitmap.InstallPixels(info, handle.AddrOfPinnedObject(), info.RowBytes);

                    if (!success)
                    {
                        // 安裝失敗，跳過或記錄錯誤
                        continue;
                    }

                    using var skImage = SKImage.FromBitmap(bitmap);
                    using var encoded = skImage.Encode(SKEncodedImageFormat.Jpeg, 92);

                    if (encoded == null) continue;

                    string outPath = Path.Combine(UploadImagesPath, $"{pdfName}_p{i + 1:D3}.jpg");
                    Directory.CreateDirectory(UploadImagesPath);

                    await File.WriteAllBytesAsync(outPath, encoded.ToArray());
                    generated.Add(outPath);
                }
                finally
                {
                    handle.Free();  // 務必釋放 pinned 記憶體
                }
            }

            return generated;
        }

        private void LoadCurrentImage()
        {
            if (CurrentImageIndex < 0 || CurrentImageIndex >= ImagePaths.Count) return;

            CurrentImagePath = ImagePaths[CurrentImageIndex];

            try
            {
                CurrentImage = new Bitmap(CurrentImagePath);
                Annotations.Clear();
                if (_canvas != null) _canvas.Children.Clear();
                StatusText = $"目前：{Path.GetFileName(CurrentImagePath)}  ({CurrentImageIndex + 1}/{ImagePaths.Count})";
                UpdateImageInfo();
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
            string txtPath = Path.Combine(SaveAnnotationsPath, name + ".txt");

            try
            {
                Directory.CreateDirectory(SaveAnnotationsPath);

                using var sw = new StreamWriter(txtPath);
                foreach (var ann in Annotations)
                {
                    if (ann.Type == AnnotationType.BoundingBox && ann.Points.Count >= 2)
                    {
                        var box = ann.GetBoundingBox();  // 請確保 Annotation 有此方法
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