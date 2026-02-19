using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using OB.ViewModels;
using OB.ViewModels.Dialogs;
using OB.Views;
using OB.Views.Dialogs;
using Prism.Dialogs;
using Prism.DryIoc;
using Prism.Ioc;
using System;
using System.Threading.Tasks;

namespace OB
{
    public partial class App : PrismApplication
    {
        protected override AvaloniaObject CreateShell()
        {
            // 返回 null 表示不由 Prism 自动显示 Shell
            return null;
        }

        protected override void RegisterTypes(IContainerRegistry containerRegistry)
        {
            // 注册对话框
            containerRegistry.RegisterDialog<Login, LoginViewModel>();
            // 注册导航窗口
            containerRegistry.RegisterForNavigation<MainWin, MainViewModel>();
        }

        public override void OnFrameworkInitializationCompleted()
        {
            if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktopLifetime)
            {
                // 启动自定义登录流程
                StartWithLoginAsync(desktopLifetime);
            }

            base.OnFrameworkInitializationCompleted();
        }

        private async void StartWithLoginAsync(IClassicDesktopStyleApplicationLifetime desktopLifetime)
        {
            // 1. 创建一个隐藏的父窗口，用于作为登录对话框的 Owner
            var hiddenOwner = new Window
            {
                Width = 0,
                Height = 0,
                WindowState = WindowState.Minimized,
                ShowInTaskbar = false,
                ShowActivated = false
            };
            hiddenOwner.Show();

            // 2. 创建 LoginViewModel 实例（使用容器解析，以支持依赖注入）
            var loginVM = Container.Resolve<LoginViewModel>();

            // 3. 创建 Login 用户控件并设置 DataContext
            var loginControl = new Login();
            loginControl.DataContext = loginVM;

            // 4. 创建登录窗口，将 Login 控件作为内容
            var loginWindow = new Window
            {
                Content = loginControl,
                SizeToContent = SizeToContent.WidthAndHeight,
                CanResize = false,
                WindowStartupLocation = WindowStartupLocation.CenterScreen,
                Title = loginVM.Title
            };

            // 用于标记登录是否成功
            var loginSucceeded = false;

            // 5. 订阅 LoginClosed 事件
            loginVM.LoginClosed += (sender, result) =>
            {
                if (result == ButtonResult.OK)
                {
                    loginSucceeded = true;
                    // 登录成功：创建并显示主窗口
                    var mainWin = Container.Resolve<MainWin>();
                    desktopLifetime.MainWindow = mainWin;
                    mainWin.Show();
                }
                // 关闭登录窗口
                loginWindow.Close();
            };

            // 6. 处理用户直接关闭登录窗口（点击 X）的情况
            loginWindow.Closed += (sender, e) =>
            {
                if (!loginSucceeded)
                {
                    // 登录未成功即关闭窗口，退出应用
                    desktopLifetime.Shutdown();
                }
                // 关闭隐藏的父窗口
                hiddenOwner.Close();
            };

            // 7. 显示登录对话框（模态）
            await loginWindow.ShowDialog(hiddenOwner);
        }
    }
}