using Material.Icons;
using OB.Models;
using OB.Tools;
using Prism.Commands;
using Prism.Mvvm;
using Prism.Navigation;
using Prism.Navigation.Regions;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;

namespace OB.ViewModels
{
    public class MainViewModel : BindableBase
    {
        #region 字段
        private bool _isMenuExpanded = true;
        private LeftMenuItem _selectedMenuItem;
        private LogUserInfo logUser;
        private RemoteDBTools remoteDBTools;
        private readonly IRegionManager regionManager;
        private IRegionNavigationJournal? journal;
        #endregion

        #region 屬性
        public RemoteDBTools RemoteDBTools
        {
            get => remoteDBTools;
            set => SetProperty(ref remoteDBTools, value);
        }

        public LogUserInfo LogUser
        {
            get => logUser;
            set => SetProperty(ref logUser, value);
        }

        public bool IsMenuExpanded
        {
            get => _isMenuExpanded;
            set => SetProperty(ref _isMenuExpanded, value);
        }

        public LeftMenuItem SelectedMenuItem
        {
            get => _selectedMenuItem;
            set => SetProperty(ref _selectedMenuItem, value);
        }

        public ObservableCollection<LeftMenuItem> MenuItems { get; }
        public DelegateCommand ToggleMenuCommand { get; }
        public DelegateCommand<LeftMenuItem> SelectMenuItemCommand { get; }
        #endregion

        public MainViewModel(IRegionManager regionManager)
        {
            this.regionManager = regionManager;

            logUser = new LogUserInfo();

            MenuItems = new ObservableCollection<LeftMenuItem>
            {
                new LeftMenuItem { Icon = MaterialIconKind.Home, Title = "首頁", ViewName = "Home" },
                new LeftMenuItem { Icon = MaterialIconKind.CogOutline, Title = "設置", ViewName = "Settings" }
            };

            ToggleMenuCommand = new DelegateCommand(() => IsMenuExpanded = !IsMenuExpanded);

        
            SelectMenuItemCommand = new DelegateCommand<LeftMenuItem>(menuItem => _ = NavigateAsync(menuItem));

            SelectedMenuItem = MenuItems.FirstOrDefault();
        }

        #region 導航實現
        public async Task NavigateAsync(LeftMenuItem menuItem)
        {
            if (menuItem == null || string.IsNullOrEmpty(menuItem.ViewName))
                return;

            var parameters = new NavigationParameters();
            if (menuItem.ViewName == "Home" && LogUser != null)
            {
                parameters.Add("LogUser", LogUser);
                parameters.Add("dbtools", RemoteDBTools);
            }
            if (menuItem.ViewName == "Settings" && LogUser != null)
            {
                parameters.Add("LogUser", LogUser);
                parameters.Add("dbtools", RemoteDBTools);
            }
            regionManager.Regions["MainRegion"].RequestNavigate(
                menuItem.ViewName,
                callback => journal = callback.Context.NavigationService.Journal,
                parameters);
        }

        // 默認導航首頁（供 App.cs 等地方呼叫）
        public async Task DefaultNavigateAsync()
        {
            var parameters = new NavigationParameters();
            if (LogUser != null)
            {
                parameters.Add("LogUser", LogUser);
                parameters.Add("dbtools", RemoteDBTools);
            }

            regionManager.Regions["MainRegion"].RequestNavigate(
                "Home",
                callback => journal = callback.Context.NavigationService.Journal,
                parameters);
        }
        #endregion
    }
}