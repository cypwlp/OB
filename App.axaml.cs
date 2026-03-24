using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Threading;
using OB.Models;
using OB.Services.impls;
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
using System.Threading;
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
            containerRegistry.RegisterDialog<About, AboutViewModel>();
            containerRegistry.RegisterDialog<UploadDialog, UploadViewModel>();
            containerRegistry.RegisterDialog<ModelClass, ModelClassViewModel>();
            containerRegistry.RegisterForNavigation<AutoDet, AutoDetViewModel>();
            containerRegistry.RegisterForNavigation<OnnxModelMS,OnnxModelMSViewModel>();
        }

        public override void OnFrameworkInitializationCompleted()
        {
            if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktopLifetime)
            {
                _ = StartWithLoginAsync(desktopLifetime);
            }
            base.OnFrameworkInitializationCompleted();
        }

        private async Task CheckForUpdatesAsync()
        {
            string countryCode = null;
            using (var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15)))
            {
                try
                {
                    var geoService = new GeoLocationService();
                    countryCode = await geoService.GetCountryCodeAsync(cts.Token);
                }
                catch (OperationCanceledException)
                {

                }
            }

            try
            {
                //bool useChinaMirror = countryCode == "CN";
                //IUpdateSource source;
                //if (useChinaMirror)
                //{
                //    source = new SimpleFileSource(new System.IO.DirectoryInfo("http://129.204.149.106:8080/releases"));
                //}
                //else
                //{
                //    source = new GithubSource("https://github.com/cypwlp/OB", "", false);
                //}
                var source = new GithubSource("https://github.com/cypwlp/OB", "", false);
                var mgr = new UpdateManager(source);
                var updateInfo = await mgr.CheckForUpdatesAsync();
                if (updateInfo == null)
                {
                    return;
                }
                await Dispatcher.UIThread.InvokeAsync(async () =>
                {
                    var dialogService = Container.Resolve<IDialogService>();
                    var parameters = new DialogParameters
            {
                { "UpdateInfo", updateInfo }
            };
                    var result = await dialogService.ShowDialogAsync("UpdateDialog", parameters);

                    if (result?.Result == ButtonResult.OK)
                    {
                        await mgr.DownloadUpdatesAsync(updateInfo);
                        mgr.ApplyUpdatesAndRestart(updateInfo);
                    }
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Velopack 更新檢查失敗: {ex.Message}");
            }
        }

        private async Task StartWithLoginAsync(IClassicDesktopStyleApplicationLifetime desktopLifetime)
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