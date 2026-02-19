using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Media;
using OB.ViewModels;
using OB.ViewModels.Dialogs;
using OB.Views;
using OB.Views.Dialogs;
using Prism.Dialogs;
using Prism.DryIoc;
using Prism.Ioc;
// ... 其他 using

namespace OB
{
    public partial class App : PrismApplication
    {
        protected override AvaloniaObject CreateShell() => null;

        protected override void RegisterTypes(IContainerRegistry containerRegistry)
        {
            containerRegistry.RegisterDialog<Login, LoginViewModel>();
            containerRegistry.RegisterForNavigation<MainWin, MainViewModel>();
        }

        public override void OnFrameworkInitializationCompleted()
        {
            if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktopLifetime)
            {
                StartWithLoginAsync(desktopLifetime);
            }
            base.OnFrameworkInitializationCompleted();
        }

        private void StartWithLoginAsync(IClassicDesktopStyleApplicationLifetime desktopLifetime)
        {
            // 1. 创建一个极小的隐藏窗口作为临时所有者
            var splashWindow = new Window
            {
                Width = 1,
                Height = 1,
                WindowStartupLocation = WindowStartupLocation.CenterScreen,
                SystemDecorations = SystemDecorations.None,
                Background = new SolidColorBrush(Colors.White),
                ShowInTaskbar = false,
                ShowActivated = false,
                IsHitTestVisible = false
            };
            splashWindow.Show();               // 必须显示才能成为有效所有者
            desktopLifetime.MainWindow = splashWindow; // 暂时设置为应用主窗口

            var dialogService = Container.Resolve<IDialogService>();
            var parameters = new DialogParameters();

            // 2. 显示登录对话框，owner 为 splashWindow
            dialogService.ShowDialog("Login", parameters, result =>
            {
                if (result.Result == ButtonResult.OK)
                {
                    // 登录成功：创建真实主窗口并显示
                    var mainWin = Container.Resolve<MainWin>();
                    mainWin.Show();
                    desktopLifetime.MainWindow = mainWin; // 更新主窗口引用
                    splashWindow.Close();                   // 关闭临时窗口
                }
                else
                {
                    // 登录取消或失败：关闭临时窗口并退出应用
                    splashWindow.Close();
                    desktopLifetime.Shutdown();
                }
            });
        }
    }
}