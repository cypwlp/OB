using DryIoc;
using OB.Models;
using Prism.Commands;
using Prism.Dialogs;
using Prism.Ioc;
using Prism.Mvvm;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace OB.ViewModels
{
    public class SettingsViewModel : BindableBase
    {
        private int _selectedIndex;

        public int SelectedIndex
        {
            get => _selectedIndex;
            set => SetProperty(ref _selectedIndex, value);
        }

        private ObservableCollection<YoloClassSetting> _classSettings = new();
        public ObservableCollection<YoloClassSetting> ClassSettings
        {
            get => _classSettings;
            set => SetProperty(ref _classSettings, value);
        }

        // 新增的開啟對話框命令
        public DelegateCommand OpenDialogCommand { get; }

        public SettingsViewModel()
        {
            OpenDialogCommand = new DelegateCommand(OpenDialog, CanOpenDialog)
                .ObservesProperty(() => SelectedIndex); // 當 SelectedIndex 改變時自動更新命令可否執行
            LoadSettings();
        }

        // 判斷是否可以開啟對話框（必須有選取的列）
        private bool CanOpenDialog()
        {
            return SelectedIndex >= 0 && SelectedIndex < ClassSettings.Count;
        }

        // 開啟對話框，並根據選取項傳遞不同參數
        private async void OpenDialog()
        {
            if (!CanOpenDialog()) return;

            var selectedSetting = ClassSettings[SelectedIndex];
            var parameters = new DialogParameters
            {
                { "setting", selectedSetting },   // 將選取的設定物件傳給對話框
                { "index", SelectedIndex }        // 也可傳遞索引值
            };

            // 可根據選取項的屬性決定使用哪個對話框（例如 IsAreaSensitive 或 Label）
            string dialogName = "Subclass"; // 預設對話框名稱，可依需求變更
            // 範例：若面積敏感則開啟進階設定對話框，否則開啟簡單對話框
            // dialogName = selectedSetting.IsAreaSensitive ? "AreaSensitiveDialog" : "SimpleDialog";

            var dialogService = ContainerLocator.Container.Resolve<IDialogService>();
            var result = await dialogService.ShowDialogAsync(dialogName, parameters);

            if (result.Result == ButtonResult.OK)
            {
                // 對話框回傳成功後可儲存設定
                SaveSettings();
            }
        }

        public void LoadSettings()
        {
            if (!string.IsNullOrEmpty(OB.Default.mYoloLabels))
            {
                try
                {
                    var list = JsonSerializer.Deserialize<ObservableCollection<YoloClassSetting>>(OB.Default.mYoloLabels);
                    if (list != null) { ClassSettings = list; return; }
                }
                catch { }
            }
            ClassSettings = new ObservableCollection<YoloClassSetting>();
        }

        public void SaveSettings()
        {
            string json = JsonSerializer.Serialize(ClassSettings);
            OB.Default.mYoloLabels = json;
            OB.Default.Save();
        }

        public void SyncLabels(string[] labels)
        {
            bool changed = false;
            foreach (var label in labels)
            {
                if (!ClassSettings.Any(x => x.Label == label))
                {
                    ClassSettings.Add(new YoloClassSetting { Label = label });
                    changed = true;
                }
            }
            if (changed) SaveSettings();
        }

        // 保留原有的方法（如有需要可繼續使用，或視情況移除）
        public async Task ShowSubClassDialogAsync()
        {
            var parameters = new DialogParameters();
            var dialogService = ContainerLocator.Container.Resolve<IDialogService>();
            await dialogService.ShowDialogAsync("Subclass", parameters);
        }
    }
}