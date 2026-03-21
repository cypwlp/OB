using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Markup.Xaml;
using OB.ViewModels;

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

        // 以下事件處理保持不變
        private void DrawingCanvas_PointerPressed(object? sender, PointerPressedEventArgs e)
        {
            if (DataContext is AutoDetViewModel vm) vm.OnPointerPressed(e);
        }

        private void DrawingCanvas_PointerMoved(object? sender, PointerEventArgs e)
        {
            if (DataContext is AutoDetViewModel vm) vm.OnPointerMoved(e);
        }

        private void DrawingCanvas_PointerReleased(object? sender, PointerReleasedEventArgs e)
        {
            if (DataContext is AutoDetViewModel vm) vm.OnPointerReleased(e);
        }

        private void DrawingCanvas_PointerRightPressed(object? sender, PointerPressedEventArgs e)
        {
            if (DataContext is AutoDetViewModel vm) vm.OnPointerRightPressed(e);
        }
    }
}