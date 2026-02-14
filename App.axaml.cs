using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Microsoft.Extensions.Configuration;
using OB.Tools;
using OB.ViewModels;
using OB.ViewModels.Dialogs;
using OB.Views;
using OB.Views.Dialogs;
using Prism.DryIoc;
using Prism.Ioc;
using System.IO;

namespace OB
{
    public partial class App : PrismApplication
    {

        //public string UserName { get; set; }   
        //public string Password { get; set; }
        protected override AvaloniaObject CreateShell()
        {
            var mainWindow = Container.Resolve<MainWin>();
            return mainWindow;
        }

        protected override void RegisterTypes(IContainerRegistry containerRegistry)
        {

            // ³õÊ¼»¯DBToolsµ¥Àý
            //DBTools.Instance.Initialize(UserName, Password);

            //Ô]ƒÔµÇä›Œ¦Ô’¿ò
            containerRegistry.RegisterDialog<Login,LoginViewModel>();

            // ×¢²á´°¿Ú
            containerRegistry.RegisterForNavigation<MainWin,MainViewModel>();
            containerRegistry.RegisterForNavigation<Home, HomeViewModel>();
        }
    }
}