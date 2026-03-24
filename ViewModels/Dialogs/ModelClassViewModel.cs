using OB.Models;
using OB.Services;
using Prism.Commands;
using Prism.Dialogs;
using Prism.Mvvm;
using System;
using System.Threading.Tasks;

namespace OB.ViewModels.Dialogs
{
    public class ModelClassViewModel : BindableBase, IDialogAware
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

        public DelegateCommand CloseCommand { get; }

        public ModelClassViewModel(IOnnxModelAnalyzer analyzer)
        {
            _analyzer = analyzer;
            CloseCommand = new DelegateCommand(() => RequestClose.Invoke());
        }

        public DialogCloseListener RequestClose { get; private set; }

        public bool CanCloseDialog() => true;

        public void OnDialogClosed() { }

        public async void OnDialogOpened(IDialogParameters parameters)
        {
            var modelPath = parameters.GetValue<string>("modelPath");
            var modelName = parameters.GetValue<string>("modelName") ?? "未知模型";

            if (!string.IsNullOrEmpty(modelPath))
            {
                Title = $"模型詳情 - {modelName}";
                try
                {
                    ModelInfo = await _analyzer.AnalyzeAsync(modelPath, deepAnalysis: true);
                }
                catch (Exception ex)
                {
                    // 可自行加入錯誤處理或日誌
                    System.Diagnostics.Debug.WriteLine($"分析模型失敗: {ex.Message}");
                }
            }
        }
    }
}