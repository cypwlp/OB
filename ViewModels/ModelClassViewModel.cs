using DryIoc;
using DynamicData.Binding;
using OB.Models;
using OB.Services;
using Prism.Commands;
using Prism.Dialogs;
using Prism.Mvvm;
using Prism.Navigation.Regions;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace OB.ViewModels
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
                    await LoadClassesAsync(); 
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


        private ObservableCollection<ClassInfo> _classInfos = new();
        public ObservableCollection<ClassInfo> ClassInfos
        {
            get => _classInfos;
            set => SetProperty(ref _classInfos, value);
        }
        private async Task LoadClassesAsync()
        {
            string? raw = ModelInfo?.CustomMetadata?.GetValueOrDefault("names");
            if (string.IsNullOrWhiteSpace(raw))
                return;

            // 尝试 JSON 解析（标准格式如 {"0":"we","1":"re"}）
            try
            {
                var dict = JsonSerializer.Deserialize<Dictionary<int, string>>(raw);
                if (dict != null)
                {
                    var result = new ObservableCollection<ClassInfo>();
                    foreach (var kv in dict)
                    {
                        result.Add(new ClassInfo { Suffix = kv.Key, ClassName = kv.Value });
                    }
                    ClassInfos = result;
                    return; // 成功则直接返回
                }
            }
            catch (JsonException)
            {
                // JSON 解析失败，继续尝试手动解析
            }

            // 手动解析（处理类似 "0:we,1:re" 或 "{0:we,1:re}" 的格式）
            // 1. 去除首尾可能的花括号
            raw = raw.Trim();
            if (raw.StartsWith("{") && raw.EndsWith("}"))
                raw = raw.Substring(1, raw.Length - 2);

            // 2. 按逗号分割
            var parts = raw.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
            var resultList = new ObservableCollection<ClassInfo>();

            foreach (var part in parts)
            {
                var trimmedPart = part.Trim();
                // 按冒号分割键值对
                var keyValue = trimmedPart.Split(new[] { ':' }, StringSplitOptions.RemoveEmptyEntries);
                if (keyValue.Length == 2 &&
                    int.TryParse(keyValue[0].Trim(), out int suffix))
                {
                    string className = keyValue[1].Trim();

                    // 去除可能包裹的引号
                    if (className.StartsWith("\"") && className.EndsWith("\""))
                        className = className.Substring(1, className.Length - 2);
                    else if (className.StartsWith("'") && className.EndsWith("'"))
                        className = className.Substring(1, className.Length - 2);

                    // 去除残留的 }（如 "re}" 的情况）
                    if (className.EndsWith("}"))
                        className = className.TrimEnd('}');

                    resultList.Add(new ClassInfo
                    {
                        Suffix = suffix,
                        ClassName = className
                    });
                }
            }

            ClassInfos = resultList;
        }
    }
}