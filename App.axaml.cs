using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using NetSparkleUpdater;
using NetSparkleUpdater.Enums;
using NetSparkleUpdater.SignatureVerifiers;
using NetSparkleUpdater.UI.Avalonia;
using OB.Models;
using OB.Tools;
using OB.ViewModels;
using OB.ViewModels.Dialogs;
using OB.Views;
using OB.Views.Dialogs;
using Prism.Dialogs;
using Prism.DryIoc;
using Prism.Ioc;
using Prism.Navigation.Regions;
using System;
using System.Threading.Tasks;

namespace OB
{
    public partial class App : PrismApplication
    {
        // 建議將 SparkleInstance 改為可空的，以便後續判斷
        public static SparkleUpdater? SparkleInstance { get; private set; }

        protected override AvaloniaObject CreateShell() => null!;

        protected override void RegisterTypes(IContainerRegistry containerRegistry)
        {
            containerRegistry.RegisterDialog<Login, LoginViewModel>();
            containerRegistry.RegisterForNavigation<MainWin, MainViewModel>();
            containerRegistry.RegisterForNavigation<Home, HomeViewModel>();
            containerRegistry.RegisterForNavigation<Settings, SettingsViewModel>();
            containerRegistry.RegisterForNavigation<Detect, DetectViewModel>();
            containerRegistry.RegisterForNavigation<Process, ProcessViewModel>();
        }

        public override void OnFrameworkInitializationCompleted()
        {
            if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktopLifetime)
            {
                // 1. 非同步啟動更新檢查
                _ = CheckForUpdatesAsync();

                // 2. 啟動登入視窗邏輯
                StartWithLoginAsync(desktopLifetime);
            }
            base.OnFrameworkInitializationCompleted();
        }

        /// <summary>
        /// 檢查更新的核心邏輯
        /// </summary>
        public async Task CheckForUpdatesAsync()
        {
            try
            {
                // 配置 NetSparkle (適用於 v3.0.3)
                SparkleInstance = new SparkleUpdater(
                    "https://github.com/cypwlp/OB/releases/latest/download/appcast.xml",
                    new Ed25519Checker(SecurityMode.Strict, "NTdAnzITw24xQe8KU4maWo193JQk/uIu/lJyJ5C4u3I=")
                )
                {
                    // 使用 Avalonia 專屬介面
                    UIFactory = new NetSparkleUpdater.UI.Avalonia.UIFactory(),
                };

                // 在 v3.x 中，CheckForUpdatesQuietly 會直接執行檢查，無視之前的檢查時間。
                // 如果有新版本會彈窗提示，無新版本則安靜。
                await SparkleInstance.CheckForUpdatesQuietly();
            }
            catch (Exception ex)
            {
                // 如果是調試模式可以印出來
                System.Diagnostics.Debug.WriteLine($"Update check failed: {ex.Message}");
            }
        }

        private void StartWithLoginAsync(IClassicDesktopStyleApplicationLifetime desktopLifetime)
        {
            // 初始化一個透明的小視窗作為 MainWindow 容器（確保生命週期啟動）
            var splashWindow = new Window { Width = 1, Height = 1, SystemDecorations = SystemDecorations.None, ShowInTaskbar = false, Opacity = 0 };
            splashWindow.Show();
            desktopLifetime.MainWindow = splashWindow;

            var dialogService = Container.Resolve<IDialogService>();
            dialogService.ShowDialog("Login", null, async result =>
            {
                if (result.Result == ButtonResult.OK)
                {
                    if (result.Parameters.TryGetValue<RemoteDBTools>("dbtools", out var dbtools) &&
                        result.Parameters.TryGetValue<LogUserInfo>("LogUser", out var LogUser))
                    {
                        // --- 重點修復部分：確保 DataContext 被賦值 ---
                        var mainWin = Container.Resolve<MainWin>();
                        // 手動從容器中取得 MainViewModel
                        var vm = Container.Resolve<MainViewModel>();

                        // 設置 DataContext
                        mainWin.DataContext = vm;

                        vm.LogUser = LogUser;
                        vm.RemoteDBTools = dbtools;

                        var regionManager = Container.Resolve<IRegionManager>();
                        RegionManager.SetRegionManager(mainWin, regionManager);

                        // 在關閉 Splash 之前切換 MainWindow 並顯示
                        mainWin.Show();
                        desktopLifetime.MainWindow = mainWin;

                        await vm.DefaultNavigateAsync();

                        splashWindow.Close();
                        // ------------------------------------------
                    }
                    else
                    {
                        splashWindow.Close();
                        desktopLifetime.Shutdown();
                    }
                }
                else
                {
                    splashWindow.Close();
                    desktopLifetime.Shutdown();
                }
            });
        }
    }
}