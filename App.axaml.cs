using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Prism.DryIoc;
using Prism.Ioc;
using OB.ViewModels;
using OB.Views;
using OB.Views.Dialogs;
using OB.ViewModels.Dialogs;

namespace OB
{
    public partial class App : PrismApplication
    {
        protected override AvaloniaObject CreateShell()
        {
            var mainWindow = Container.Resolve<MainWin>();
            return mainWindow;
        }

        protected override void RegisterTypes(IContainerRegistry containerRegistry)
        {

            //Ô]ƒÔµÇä›Œ¦Ô’¿ò
            containerRegistry.RegisterDialog<Login,LoginViewModel>();

            // ×¢²á´°¿Ú
            containerRegistry.RegisterForNavigation<MainWin,MainViewModel>();
            containerRegistry.RegisterForNavigation<Home, HomeViewModel>();
        }
    }
}