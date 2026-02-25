using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Threading;
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
using Velopack;
using Velopack.Sources;

namespace OB
{
    public partial class App : PrismApplication
    {
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
                // 使用 GitHub 作為更新源
                // 注意：repoUrl 格式為 "https://github.com/你的用戶名/你的倉庫名" （不要加 .git）
                var source = new GithubSource("https://github.com/cypwlp/OB", "", false);
                var mgr = new UpdateManager(source);

                // 檢查是否有更新
                var updateInfo = await mgr.CheckForUpdatesAsync();

                if (updateInfo == null)
                {
                    // 已是最新版，可記 log 或忽略
                    return;
                }

                // 有更新 → 切到 UI 執行緒顯示對話框
                await Dispatcher.UIThread.InvokeAsync(async () =>
                {
                    var dialogService = Container.Resolve<IDialogService>();
                    var parameters = new DialogParameters
            {
                { "UpdateInfo", updateInfo }
            };

                    // 顯示你的自訂更新對話框
                    var result = await dialogService.ShowDialogAsync("UpdateDialog", parameters);

                    if (result?.Result == ButtonResult.OK)
                    {
                        // 使用者同意更新 → 先下載 → 然後套用並重啟
                        await mgr.DownloadUpdatesAsync(updateInfo);

                        // 套用更新並重啟應用（會自動關閉目前程式並啟動新版）
                        mgr.ApplyUpdatesAndRestart(updateInfo);
                    }
                });
            }
            catch (Exception ex)
            {
                // 避免更新失敗導致程式崩潰，記錄到輸出視窗
                System.Diagnostics.Debug.WriteLine($"Velopack 更新檢查失敗: {ex.Message}");
            }
        }

        private void StartWithLoginAsync(IClassicDesktopStyleApplicationLifetime desktopLifetime)
        {
            var splashWindow = new Window
            {
                Width = 1,
                Height = 1,
                SystemDecorations = SystemDecorations.None,
                ShowInTaskbar = false,
                Opacity = 0
            };
            splashWindow.Show();
            desktopLifetime.MainWindow = splashWindow;

            var dialogService = Container.Resolve<IDialogService>();

            dialogService.ShowDialog("Login", null, async result =>
            {
                if (result?.Result == ButtonResult.OK)
                {
                    if (result.Parameters.TryGetValue<RemoteDBTools>("dbtools", out var dbtools) &&
                        result.Parameters.TryGetValue<LogUserInfo>("LogUser", out var logUser))
                    {
                        var mainWin = Container.Resolve<MainWin>();
                        var vm = Container.Resolve<MainViewModel>();
                        vm.LogUser = logUser;
                        vm.RemoteDBTools = dbtools;
                        mainWin.DataContext = vm;

                        var regionManager = Container.Resolve<IRegionManager>();
                        RegionManager.SetRegionManager(mainWin, regionManager);

                        mainWin.Show();
                        desktopLifetime.MainWindow = mainWin;

                        await vm.DefaultNavigateAsync();

                        // 登入成功後立即檢查更新（最推薦的位置）
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