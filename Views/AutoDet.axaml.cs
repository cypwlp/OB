using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Markup.Xaml;
using Avalonia.Platform.Storage;
using OB.ViewModels;
using System.Threading.Tasks;

namespace OB.Views
{
    public partial class AutoDet : UserControl
    {
        public AutoDet()
        {
            InitializeComponent();
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }

        private async void LoadButton_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            if (DataContext is not AutoDetViewModel vm) return;

            var top = TopLevel.GetTopLevel(this);
            if (top == null) return;

            var folders = await top.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
            {
                AllowMultiple = false,
                Title = "ﬂxìÒ PDF ŸY¡œäA"
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
                vm.OnPointerPressedRight(e);  // Ãé¿Ì”“ÊI
            }
        }

        private void DrawingCanvas_PointerMoved(object? sender, PointerEventArgs e)
        {
            if (DataContext is AutoDetViewModel vm) vm.OnPointerMoved(e);
        }

        private void DrawingCanvas_PointerReleased(object? sender, PointerReleasedEventArgs e)
        {
            if (DataContext is AutoDetViewModel vm) vm.OnPointerReleased(e);
        }
    }
}