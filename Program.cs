using Avalonia;
using OB.Views;
using ReactiveUI.Avalonia;
using System;

namespace OB
{
    internal sealed class Program
    {
        // Initialization code. Don't use any Avalonia, third-party APIs or any
        // SynchronizationContext-reliant code before AppMain is called: things aren't initialized
        // yet and stuff might break.
        [STAThread]
        public static void Main(string[] args) => BuildAvaloniaApp()
            .StartWithClassicDesktopLifetime(args);

        // Avalonia configuration, don't remove; also used by visual designer.
        public static AppBuilder BuildAvaloniaApp()
            => AppBuilder.Configure<App>()
                .UsePlatformDetect()
                .WithInterFont()
                .LogToTrace()
                .UseReactiveUI();

        //private void ShowLoginDialog(DesktopLifetime desktopLifetime)
        //{
        //    var dialogService = Container.Resolve<IDialogService>();
        //    var parameters = new DialogParameters();
        //    dialogService.ShowDialog("Login", parameters, r =>
        //    {
        //        if (r?.Result == ButtonResult.OK)
        //        {
        //            var mainWin = Container.Resolve<MainWin>();
        //            desktopLifetime.MainWindow = mainWin;
        //            mainWin.Show();
        //        }
        //        else
        //        {
        //            desktopLifetime.Shutdown();
        //        }
        //    });
        //}

    }
}
