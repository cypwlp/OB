using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using NetSparkleUpdater;
using NetSparkleUpdater.Enums;
using NetSparkleUpdater.SignatureVerifiers;
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
using System.Net;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace OB
{
    public partial class App : PrismApplication
    {
        private static SparkleUpdater? _sparkle;
        private static bool _isUpdateReady = false;
        private static AppCastItem? _updateItem;
        private static string? _updateInstallerPath;
        private static StringBuilder _debugLogs = new StringBuilder();

        public static event EventHandler? UpdateReadyToInstall;
        public static bool IsUpdateReady => _isUpdateReady;

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
                StartWithLoginAsync(desktopLifetime);
            }
            base.OnFrameworkInitializationCompleted();
        }

        private async Task CheckForUpdatesAsync()
        {
            _debugLogs.Clear();
            _debugLogs.AppendLine("--- Update Diagnostic Report ---");

            try
            {
                ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12 | SecurityProtocolType.Tls13;
                string appcastUrl = @"https://github.com/cypwlp/OB/releases/latest/download/appcast.xml";

                var assembly = Assembly.GetEntryAssembly();
                var currentVersion = assembly?.GetName().Version ?? new Version(0, 0, 0, 0);
                string assemblyPath = System.Environment.ProcessPath ?? assembly?.Location ?? "";

                _debugLogs.AppendLine("[Local Version] " + currentVersion.ToString());
                _debugLogs.AppendLine("[App Path] " + assemblyPath);

                _sparkle = new SparkleUpdater(appcastUrl, new Ed25519Checker(SecurityMode.Unsafe), assemblyPath)
                {
                    UIFactory = null,
                    RelaunchAfterUpdate = true
                };

                // NetSparkle 3.0.3 Events
                _sparkle.DownloadStarted += (item, path) => _debugLogs.AppendLine("[Download] Started: " + item.Version);

                _sparkle.DownloadFinished += (item, path) =>
                {
                    _debugLogs.AppendLine("[Download] Success! Path: " + path);
                    _updateInstallerPath = path;
                    _isUpdateReady = true;
                    UpdateReadyToInstall?.Invoke(null, EventArgs.Empty);
                };

                _sparkle.DownloadHadError += (item, path, exception) =>
                {
                    string errMsg = "[Error] Download Failed: " + exception.Message;
                    _debugLogs.AppendLine(errMsg);
                    ShowDebugError("Download Error", errMsg);
                };

                var updateInfo = await _sparkle.CheckForUpdatesQuietly();
                _debugLogs.AppendLine("[Status] " + updateInfo.Status.ToString());

                if (updateInfo.Status == UpdateStatus.UpdateAvailable && updateInfo.Updates?.Count > 0)
                {
                    _updateItem = updateInfo.Updates[0];
                    _debugLogs.AppendLine("[New Version Found] " + _updateItem.Version);

                    if (new Version(_updateItem.Version) > currentVersion)
                    {
                        _debugLogs.AppendLine("[Action] Initializing background download...");
                        await _sparkle.InitAndBeginDownload(_updateItem);
                    }
                    else
                    {
                        _debugLogs.AppendLine("[Skip] New version is not higher than local.");
                    }
                }
                else
                {
                    _debugLogs.AppendLine("[Result] No update needed. Status: " + updateInfo.Status.ToString());
                }
            }
            catch (Exception ex)
            {
                _debugLogs.AppendLine("[Crash] " + ex.Message);
                ShowDebugError("Check Update Exception", _debugLogs.ToString());
            }
        }

        private void ShowDebugError(string title, string content)
        {
            Dispatcher.UIThread.Post(async () =>
            {
                var tipText = new TextBlock
                {
                    Text = "Update Notification (Diagnostic)",
                    Margin = new Thickness(10),
                    Foreground = Brushes.Red,
                    FontWeight = FontWeight.Bold
                };
                DockPanel.SetDock(tipText, Dock.Top);

                var logBox = new TextBox
                {
                    Text = content,
                    IsReadOnly = true,
                    AcceptsReturn = true,
                    TextWrapping = TextWrapping.Wrap,
                    Margin = new Thickness(10)
                };

                var rootPanel = new DockPanel();
                rootPanel.Children.Add(tipText);
                rootPanel.Children.Add(logBox);

                var win = new Window
                {
                    Title = "Debug - " + title,
                    Width = 600,
                    Height = 450,
                    WindowStartupLocation = WindowStartupLocation.CenterScreen,
                    Content = rootPanel
                };

                if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
                    await win.ShowDialog(desktop.MainWindow ?? win);
            });
        }

        public static void InstallUpdate()
        {
            if (_sparkle != null && _updateItem != null && _updateInstallerPath != null)
                _sparkle.InstallUpdate(_updateItem, _updateInstallerPath);
        }

        private void StartWithLoginAsync(IClassicDesktopStyleApplicationLifetime desktopLifetime)
        {
            var splashWindow = new Window { Width = 1, Height = 1, SystemDecorations = SystemDecorations.None, ShowInTaskbar = false };
            splashWindow.Show();
            desktopLifetime.MainWindow = splashWindow;

            _ = CheckForUpdatesAsync();

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
                        splashWindow.Close();
                    }
                    else { splashWindow.Close(); desktopLifetime.Shutdown(); }
                }
                else { splashWindow.Close(); desktopLifetime.Shutdown(); }
            });
        }
    }
}