using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Threading;
using DryIoc.ImTools;
using NetSparkleUpdater;
using NetSparkleUpdater.Enums;
using NetSparkleUpdater.SignatureVerifiers;
using NetSparkleUpdater.UI.Avalonia; // 確保有引用這個
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
using System.Collections.Generic; // 必須引用，為了使用 List<>
using System.Net;
using System.Reflection;
using System.Threading.Tasks;
using Velopack;
using Velopack.Sources;

namespace OB
{
    public partial class App : PrismApplication
    {
        // 將 Sparkle 暴露出來，方便在設置頁面手動觸發檢查
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
            containerRegistry.RegisterDialog<UpdateDialog, UpdateViewModel>();
        }

        public override void OnFrameworkInitializationCompleted()
        {
            if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktopLifetime)
            {
                StartWithLoginAsync(desktopLifetime);
            }
            base.OnFrameworkInitializationCompleted();
        }

        private async Task CheckForUpdatesAsync()
        {
            try
            {
                // 1. 配置更新源 (指向你的 GitHub Repo)
                var mgr = new UpdateManager(new GithubSource("https://github.com/cypwlp/OB", null, false));

                // 2. 檢查是否有更新
                var newVersion = await mgr.CheckForUpdatesAsync();
                if (newVersion == null) return;

                // 3. 切換到 UI 線程彈出通知
                Dispatcher.UIThread.Post(() =>
                {
                    var dialogService = Container.Resolve<IDialogService>();
                    var parameters = new DialogParameters { { "UpdateInfo", newVersion } };

                    dialogService.ShowDialog("UpdateDialog", parameters, async result =>
                    {
                        if (result.Result == ButtonResult.OK)
                        {
                            // 4. 下載更新 (增量更新會自動處理)
                            await mgr.DownloadUpdatesAsync(newVersion);

                            // 5. 套用更新並重啟
                            mgr.ApplyUpdatesAndRestart(newVersion);
                        }
                    });
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[Velopack Error] {ex.Message}");
            }
        }

        private void StartWithLoginAsync(IClassicDesktopStyleApplicationLifetime desktopLifetime)
        {
            // 創建一個透明的虛擬視窗作為初始主窗口
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
                        var mainWin = Container.Resolve<MainWin>();
                        var vm = Container.Resolve<MainViewModel>();
                        vm.LogUser = LogUser;
                        vm.RemoteDBTools = dbtools;
                        mainWin.DataContext = vm;

                        var regionManager = Container.Resolve<IRegionManager>();
                        RegionManager.SetRegionManager(mainWin, regionManager);

                        mainWin.Show();
                        desktopLifetime.MainWindow = mainWin;
                        await vm.DefaultNavigateAsync();
                        _ = CheckForUpdatesAsync();
                        splashWindow.Close();
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