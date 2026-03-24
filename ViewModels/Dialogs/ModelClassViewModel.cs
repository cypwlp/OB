using DryIoc;
using OB.Models;
using OB.Services;
using Prism.Commands;
using Prism.Dialogs;
using Prism.Mvvm;
using Prism.Navigation.Regions;
using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;

namespace OB.ViewModels.Dialogs
{
    public class ModelClassViewModel : BindableBase,INavigationAware
    {
        private readonly IOnnxModelAnalyzer _analyzer;
        private OnnxAnalysisResult? _modelInfo;

        public OnnxAnalysisResult? ModelInfo
        {
            get => _modelInfo;
            set => SetProperty(ref _modelInfo, value);
        }

        private string _title = "模型詳情";
        public string Title
        {
            get => _title;
            set => SetProperty(ref _title, value);
        }

        // 新增：自訂卡片集合
        private ObservableCollection<CustomCardItem> _customCards = new();
        public ObservableCollection<CustomCardItem> CustomCards
        {
            get => _customCards;
            set => SetProperty(ref _customCards, value);
        }

        public DelegateCommand CloseCommand { get; }

        public ModelClassViewModel(IOnnxModelAnalyzer analyzer)
        {
            _analyzer = analyzer;

        }


        public async void OnNavigatedTo(NavigationContext navigationContext)
        {
            var modelPath = navigationContext.Parameters.GetValue<string>("modelPath");
            var modelName = navigationContext.Parameters.GetValue<string>("modelName") ?? "未知模型";

            if (!string.IsNullOrEmpty(modelPath))
            {
                Title = $"模型詳情 - {modelName}";

                try
                {
                    ModelInfo = await _analyzer.AnalyzeAsync(modelPath, deepAnalysis: true);

                    // 清空並加入範例卡片（你可以改成從 ModelInfo 產生）
                    CustomCards.Clear();
                    CustomCards.Add(new CustomCardItem { Title = "準確率", Result = "98.7%", BackColor = "#4CAF50" });
                    CustomCards.Add(new CustomCardItem { Title = "平均推理時間", Result = "12.3 ms", BackColor = "#2196F3" });
                    CustomCards.Add(new CustomCardItem { Title = "參數數量", Result = "245M", BackColor = "#FF9800" });
                    CustomCards.Add(new CustomCardItem { Title = "FPS", Result = "142", BackColor = "#E91E63" });
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"分析模型失敗: {ex.Message}");
                }
            }
        }

        public bool IsNavigationTarget(NavigationContext navigationContext)
        {
            return true;
        }

        public void OnNavigatedFrom(NavigationContext navigationContext)
        {

        }

        // 卡片資料模型
        public class CustomCardItem
        {
            public string Title { get; set; } = string.Empty;
            public string Result { get; set; } = string.Empty;
            public string BackColor { get; set; } = "#FF4081";   // 顏色字串（支援 #RRGGBB 或 #AARRGGBB）
        }
    }
}