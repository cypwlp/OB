using Avalonia.Controls;
using OB.Models;
using OB.Tools;
using Prism.Commands;
using Prism.Mvvm;
using Prism.Navigation;
using Prism.Navigation.Regions;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using Material.Icons;

namespace OB.ViewModels
{
    public class MainViewModel : BindableBase
    {
        // --- 私有欄位 ---
        private bool _isMenuExpanded = true;
        private LeftMenuItem _selectedMenuItem;
        private LogUserInfo _logUser;
        private RemoteDBTools _remoteDBTools;
        private readonly IRegionManager _regionManager;
        private IRegionNavigationJournal? _journal;

        // --- 公開屬性 ---
        public RemoteDBTools RemoteDBTools
        {
            get => _remoteDBTools;
            set => SetProperty(ref _remoteDBTools, value);
        }

        public LogUserInfo LogUser
        {
            get => _logUser;
            set => SetProperty(ref _logUser, value);
        }

        public bool IsMenuExpanded
        {
            get => _isMenuExpanded;
            set => SetProperty(ref _isMenuExpanded, value);
        }

        /// <summary>
        /// 當 TreeView 選中項改變時觸發
        /// </summary>
        public LeftMenuItem SelectedMenuItem
        {
            get => _selectedMenuItem;
            set
            {
                if (SetProperty(ref _selectedMenuItem, value) && value != null)
                {
                    // 只有當 ViewName 不為空時才執行導航（父級目錄通常 ViewName 為空）
                    if (!string.IsNullOrEmpty(value.ViewName))
                    {
                        _ = NavigateAsync(value);
                    }
                }
            }
        }

        public ObservableCollection<LeftMenuItem> MenuItems { get; }

        // --- 命令 ---
        public DelegateCommand ToggleMenuCommand { get; }
        public DelegateCommand<LeftMenuItem> SelectMenuItemCommand { get; }

        // --- 構造函數 ---
        public MainViewModel(IRegionManager regionManager)
        {
            _regionManager = regionManager;
            _logUser = new LogUserInfo();

            // 1. 初始化導航選單（包含層級結構）
            MenuItems = new ObservableCollection<LeftMenuItem>
            {
                // 單級菜單
                new LeftMenuItem { Icon = MaterialIconKind.Home, Title = "首页", ViewName = "Home" },

                // 多級菜單：檢測系統
                new LeftMenuItem
                {
                    Icon = MaterialIconKind.Database,
                    Title = "檢測系統", 
                    // 注意：父項不需要設置 ViewName
                    SubItems = new ObservableCollection<LeftMenuItem>
                    {
                        new LeftMenuItem { Icon = MaterialIconKind.Magnify, Title = "實時檢測", ViewName = "Detect" },
                        new LeftMenuItem { Icon = MaterialIconKind.History, Title = "歷史數據", ViewName = "History" }
                    }
                },

                // 多級菜單：流程管理
                new LeftMenuItem
                {
                    Icon = MaterialIconKind.ChatProcessing,
                    Title = "流程管理",
                    SubItems = new ObservableCollection<LeftMenuItem>
                    {
                        new LeftMenuItem { Icon = MaterialIconKind.FileTree, Title = "當前流程", ViewName = "Process" },
                        new LeftMenuItem { Icon = MaterialIconKind.FormatListBulleted, Title = "工單列表", ViewName = "WorkOrder" }
                    }
                },

                // 單級菜單
                new LeftMenuItem { Icon = MaterialIconKind.CogOutline, Title = "設置", ViewName = "Settings" }
            };

            // 2. 命令綁定
            ToggleMenuCommand = new DelegateCommand(() => IsMenuExpanded = !IsMenuExpanded);

            // 手動觸發命令（如果 XAML 中有 Binding Command）
            SelectMenuItemCommand = new DelegateCommand<LeftMenuItem>(async menuItem =>
            {
                if (menuItem != null && !string.IsNullOrEmpty(menuItem.ViewName))
                {
                    await NavigateAsync(menuItem);
                }
            });

            // 3. 預設選中第一項（首頁）
            _selectedMenuItem = MenuItems.FirstOrDefault();
        }

        // --- 導航邏輯 ---

        /// <summary>
        /// 執行 Prism 區域導航
        /// </summary>
        public async Task NavigateAsync(LeftMenuItem menuItem)
        {
            if (menuItem == null || string.IsNullOrEmpty(menuItem.ViewName)) return;

            // 準備導航參數
            var parameters = new NavigationParameters
            {
                { "LogUser", LogUser },
                { "dbtools", RemoteDBTools }
            };

            // 執行導航到 MainRegion
            _regionManager.Regions["MainRegion"].RequestNavigate(
                menuItem.ViewName,
                callback =>
                {
                    if (callback.Success == true)
                    {
                        // 記錄導航日誌（可用於後退操作）
                        _journal = callback.Context.NavigationService.Journal;
                    }
                    else
                    {
                        // 導航失敗處理（可選）
                        System.Diagnostics.Debug.WriteLine($"導航至 {menuItem.ViewName} 失敗: {callback.Exception?.Message}");
                    }
                },
                parameters
            );
        }

        /// <summary>
        /// 默認跳轉到首頁的方法
        /// </summary>
        public async Task DefaultNavigateAsync()
        {
            var homeItem = MenuItems.FirstOrDefault(x => x.ViewName == "Home");
            if (homeItem != null)
            {
                await NavigateAsync(homeItem);
            }
        }
    }
}