using DryIoc;
using Prism.Commands;
using Prism.Dialogs;
using Prism.Ioc;
using Prism.Mvvm;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using OB.Services;

namespace OB.ViewModels
{
    public class OnnxModelMSViewModel : BindableBase
    {
        private List<FileSystemItem> _allItems = new();
        private bool _isLoading;
        public bool IsLoading
        {
            get => _isLoading;
            set => SetProperty(ref _isLoading, value);
        }

        private string _searchText = string.Empty;
        public string SearchText
        {
            get => _searchText;
            set => SetProperty(ref _searchText, value);
        }

        public ObservableCollection<FileSystemItem> Items { get; } = new();

        public DelegateCommand<FileSystemItem> ShowDetailsCommand { get; }
        public DelegateCommand SearchCommand { get; }

        private readonly string _defaultFolder = "./OnnxModel";
        private readonly IActiveModelService _activeModelService;

        public OnnxModelMSViewModel(IActiveModelService activeModelService)
        {
            _activeModelService = activeModelService;

            ShowDetailsCommand = new DelegateCommand<FileSystemItem>(async item =>
            {
                if (item == null) return;
                var parameters = new Prism.Navigation.NavigationParameters
                {
                    { "modelPath", item.FullPath },
                    { "modelName", item.Name }
                };
                //var dialogService = Prism.Ioc.ContainerLocator.Container.Resolve<IDialogService>();
                //await dialogService.ShowDialogAsync("ModelClass", parameters);
                MainViewModel mv = Prism.Ioc.ContainerLocator.Container.Resolve<MainViewModel>();
               await mv.NavigateAsync("ModelClass", parameters).ConfigureAwait(false);

            });

            SearchCommand = new DelegateCommand(ExecuteSearch);
            LoadFolderAsync(_defaultFolder).ConfigureAwait(false);
        }

        private void ExecuteSearch() => FilterItems();

        private void FilterItems()
        {
            Items.Clear();
            var filtered = string.IsNullOrWhiteSpace(SearchText)
                ? _allItems
                : _allItems.Where(i => i.Name.Contains(SearchText, StringComparison.OrdinalIgnoreCase)).ToList();

            int index = 1;
            foreach (var item in filtered)
            {
                item.Index = index++;
                item.OwnerViewModel = this;
                Items.Add(item);
            }
        }

        private async Task LoadFolderAsync(string folderPath)
        {
            string absolutePath = GetAbsolutePath(folderPath);
            if (!Directory.Exists(absolutePath))
            {
                Debug.WriteLine($"資料夾不存在: {absolutePath}");
                return;
            }

            IsLoading = true;
            await Task.Run(() =>
            {
                try
                {
                    var dirInfo = new DirectoryInfo(absolutePath);
                    var items = new List<FileSystemItem>();
                    foreach (var file in dirInfo.GetFiles("*.onnx", SearchOption.TopDirectoryOnly))
                    {
                        items.Add(new FileSystemItem
                        {
                            Name = file.Name,
                            FullPath = file.FullName,
                            Size = file.Length,
                            LastModified = file.LastWriteTime
                        });
                    }
                    _allItems = items.OrderBy(i => i.Name).ToList();
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"載入模型失敗: {ex.Message}");
                }
            });

            FilterItems();
            IsLoading = false;
        }

        private static string GetAbsolutePath(string relativePath)
        {
            if (Path.IsPathRooted(relativePath))
                return relativePath;
            if (relativePath.StartsWith("./"))
                relativePath = relativePath[2..];
            return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, relativePath);
        }

        // ====================== 模型類別 ======================
        public class FileSystemItem : BindableBase
        {
            public int Index { get; set; }
            public string Name { get; set; } = string.Empty;
            public string FullPath { get; set; } = string.Empty;
            public long? Size { get; set; }
            public DateTime LastModified { get; set; }
            public string CreatedTimeDisplay => LastModified.ToString("yyyy-MM-dd HH:mm");
            public string SizeDisplay => Size.HasValue
                ? $"{Size.Value / (1024.0 * 1024.0):0.##} MB"
                : "--";

            private bool _isEnabled;
            public bool IsEnabled
            {
                get => _isEnabled;
                set
                {
                    if (_isEnabled == value) return;
                    _isEnabled = value;
                    RaisePropertyChanged(nameof(IsEnabled));

                    if (value && OwnerViewModel != null)
                    {
                        // 互斥：關閉其他項目
                        foreach (var other in OwnerViewModel.Items)
                        {
                            if (other != this)
                            {
                                other.IsEnabled = false;
                            }
                        }

                        // 通知全域服務目前啟用的模型
                        var activeService = Prism.Ioc.ContainerLocator.Container.Resolve<IActiveModelService>();
                        activeService.SetActiveModel(this.FullPath);
                    }
                }
            }

            public OnnxModelMSViewModel? OwnerViewModel { get; set; }
        }
    }
}