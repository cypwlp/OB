using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using OB.ViewModels;
using System;
using System.Threading.Tasks;

namespace OB.Views
{
    public partial class AutoDet : UserControl
    {
        private ScaleTransform? _scaleTransform;
        private ScrollViewer? _scrollViewer;
        private Border? _zoomContainer;
        private Image? _imageViewer;
        private double _zoomFactor = 1.0;
        private const double ZoomStep = 0.1;
        private const double MinZoom = 0.1;
        private const double MaxZoom = 10.0;

        public AutoDet()
        {
            InitializeComponent();
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }

        protected override void OnDataContextChanged(EventArgs e)
        {
            base.OnDataContextChanged(e);
            if (DataContext is AutoDetViewModel vm)
            {
                _scrollViewer = this.FindControl<ScrollViewer>("ScrollViewer");
                _zoomContainer = this.FindControl<Border>("ZoomContainer");

                // 獲取 ScaleTransform（通過 ZoomContainer 的 RenderTransform）
                if (_zoomContainer != null)
                {
                    _scaleTransform = _zoomContainer.RenderTransform as ScaleTransform;
                    if (_scaleTransform != null)
                    {
                        _scaleTransform.ScaleX = _zoomFactor;
                        _scaleTransform.ScaleY = _zoomFactor;
                    }
                }

                var image = this.FindControl<Image>("ImageViewer");
                var canvas = this.FindControl<Canvas>("DrawingCanvas");

                if (image != null && canvas != null)
                {
                    vm.SetControls(image, canvas);
                    _imageViewer = image;
                }

                // 訂閱 ViewModel 的重置縮放請求
                vm.RequestResetZoom += () =>
                {
                    _zoomFactor = 1.0;
                    if (_scaleTransform != null)
                    {
                        _scaleTransform.ScaleX = _zoomFactor;
                        _scaleTransform.ScaleY = _zoomFactor;
                    }
                    if (_scrollViewer != null)
                    {
                        _scrollViewer.Offset = new Vector(0, 0);
                    }
                    vm.ZoomLevel = _zoomFactor;
                    vm.RedrawAllAnnotations();
                };
            }
        }

        // 更新縮放，可選以鼠標為中心調整滾動偏移
        private void UpdateZoom(double delta, Point? mousePos = null)
        {
            if (_scaleTransform == null || _scrollViewer == null || _zoomContainer == null) return;

            double oldZoom = _zoomFactor;
            double newZoom = _zoomFactor + delta;
            newZoom = Math.Clamp(newZoom, MinZoom, MaxZoom);
            if (Math.Abs(newZoom - oldZoom) < 0.001) return;

            Point? relativeMouse = mousePos;

            _zoomFactor = newZoom;
            _scaleTransform.ScaleX = _zoomFactor;
            _scaleTransform.ScaleY = _zoomFactor;

            // 以鼠標為中心調整滾動偏移
            if (relativeMouse.HasValue)
            {
                var oldOffset = _scrollViewer.Offset;
                var mouseInContentOld = new Point(relativeMouse.Value.X / oldZoom, relativeMouse.Value.Y / oldZoom);
                var mouseInContentNew = new Point(relativeMouse.Value.X / _zoomFactor, relativeMouse.Value.Y / _zoomFactor);
                var deltaOffset = new Vector(
                    (mouseInContentNew.X - mouseInContentOld.X) * _zoomFactor,
                    (mouseInContentNew.Y - mouseInContentOld.Y) * _zoomFactor);
                _scrollViewer.Offset = new Vector(
                    oldOffset.X + deltaOffset.X,
                    oldOffset.Y + deltaOffset.Y);
            }

            // 更新 ViewModel 的 ZoomLevel
            if (DataContext is AutoDetViewModel vm)
            {
                vm.ZoomLevel = _zoomFactor;
                vm.RedrawAllAnnotations();
            }
        }

        // 將視圖座標轉換為圖片的原始像素座標（除以縮放因子）
        private Point GetImagePixelPosition(PointerEventArgs e)
        {
            if (_zoomContainer == null || _scaleTransform == null) return default;

            var containerPos = e.GetPosition(_zoomContainer);
            double invZoom = 1.0 / _zoomFactor;
            return new Point(containerPos.X * invZoom, containerPos.Y * invZoom);
        }

        // 滾輪縮放
        private void DrawingCanvas_PointerWheelChanged(object? sender, PointerWheelEventArgs e)
        {
            var mousePos = e.GetPosition(_zoomContainer);
            double delta = e.Delta.Y > 0 ? ZoomStep : -ZoomStep;
            UpdateZoom(delta, mousePos);
            e.Handled = true;
        }

        // 鼠標按下
        private void DrawingCanvas_PointerPressed(object? sender, PointerPressedEventArgs e)
        {
            if (DataContext is not AutoDetViewModel vm) return;

            var pixelPos = GetImagePixelPosition(e);
            var point = e.GetCurrentPoint(this);
            if (point.Properties.IsRightButtonPressed)
            {
                vm.OnPointerPressedRight(pixelPos);
            }
            else
            {
                vm.OnPointerPressed(pixelPos);
            }
        }

        // 鼠標移動
        private void DrawingCanvas_PointerMoved(object? sender, PointerEventArgs e)
        {
            if (DataContext is AutoDetViewModel vm)
            {
                var pixelPos = GetImagePixelPosition(e);
                vm.OnPointerMoved(pixelPos);
            }
        }

        // 鼠標釋放
        private void DrawingCanvas_PointerReleased(object? sender, PointerReleasedEventArgs e)
        {
            if (DataContext is AutoDetViewModel vm)
            {
                var pixelPos = GetImagePixelPosition(e);
                vm.OnPointerReleased(pixelPos);
            }
        }

        // 載入 PDF 資料夾
        private async void LoadFolderButton_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            if (DataContext is not AutoDetViewModel vm) return;
            var top = TopLevel.GetTopLevel(this);
            if (top == null) return;

            var folders = await top.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
            {
                AllowMultiple = false,
                Title = "選擇 PDF 資料夾"
            });

            if (folders?.Count > 0)
            {
                await vm.ProcessPdfFolderAsync(folders[0].Path.LocalPath);
            }
        }

        // 載入單個 PDF 文件
        private async void LoadFileButton_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            if (DataContext is not AutoDetViewModel vm) return;
            var top = TopLevel.GetTopLevel(this);
            if (top == null) return;

            var files = await top.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                AllowMultiple = false,
                Title = "選擇 PDF 文件",
                FileTypeFilter = new[] { new FilePickerFileType("PDF 文件") { Patterns = new[] { "*.pdf" } } }
            });

            if (files?.Count > 0)
            {
                await vm.ProcessPdfFileAsync(files[0].Path.LocalPath);
            }
        }
    }
}