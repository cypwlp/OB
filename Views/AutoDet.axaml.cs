using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.PanAndZoom;
using Avalonia.Input;
using Avalonia.Markup.Xaml;
using Avalonia.Platform.Storage;
using OB.ViewModels;
using System;
using System.Threading.Tasks;

namespace OB.Views
{
    public partial class AutoDet : UserControl
    {
        private ZoomBorder? _zoomBorder;

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
                _zoomBorder = this.FindControl<ZoomBorder>("ZoomBorder");
                if (_zoomBorder != null)
                {
                    vm.SetZoomBorder(_zoomBorder);
                    // 监听矩阵变化，实时重绘标注
                    _zoomBorder.MatrixChanged += OnZoomMatrixChanged;
                }

                var image = this.FindControl<Image>("ImageViewer");
                var canvas = this.FindControl<Canvas>("DrawingCanvas");

                if (image != null && canvas != null)
                {
                    vm.SetControls(image, canvas);
                }
            }
        }

        private void OnZoomMatrixChanged(object? sender, EventArgs e)
        {
            if (DataContext is AutoDetViewModel vm)
            {
                vm.RedrawAllAnnotations();
            }
        }

        private async void LoadButton_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
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

        private void DrawingCanvas_PointerPressed(object? sender, PointerPressedEventArgs e)
        {
            if (DataContext is AutoDetViewModel vm)
            {
                vm.OnPointerPressed(e);
                vm.OnPointerPressedRight(e);
            }
        }

        private void DrawingCanvas_PointerMoved(object? sender, PointerEventArgs e)
        {
            if (DataContext is AutoDetViewModel vm)
            {
                vm.OnPointerMoved(e);
            }
        }

        private void DrawingCanvas_PointerReleased(object? sender, PointerReleasedEventArgs e)
        {
            if (DataContext is AutoDetViewModel vm)
            {
                vm.OnPointerReleased(e);
            }
        }
    }
}