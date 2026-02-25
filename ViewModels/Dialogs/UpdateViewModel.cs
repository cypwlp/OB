using Prism.Dialogs;
using Prism.Mvvm;
using Prism.Commands;
using Velopack;

namespace OB.ViewModels.Dialogs
{
    public class UpdateViewModel : BindableBase, IDialogAware
    {
        private UpdateInfo? _updateInfo;
        public UpdateInfo? UpdateInfo
        {
            get => _updateInfo;
            set => SetProperty(ref _updateInfo, value);
        }
        public string NewVersion => UpdateInfo?.TargetFullRelease?.Version.ToString() ?? "未知版本";

        public DialogCloseListener RequestClose { get; private set; }

        public DelegateCommand UpdateCommand { get; }
        public DelegateCommand CancelCommand { get; }

        public UpdateViewModel()
        {
            UpdateCommand = new DelegateCommand(() => RequestClose.Invoke(ButtonResult.OK));
            CancelCommand = new DelegateCommand(() => RequestClose.Invoke(ButtonResult.Cancel));
        }

        public bool CanCloseDialog() => true;
        public void OnDialogClosed() { }

        public void OnDialogOpened(IDialogParameters parameters)
        {
            if (parameters.TryGetValue<UpdateInfo>("UpdateInfo", out var info))
            {
                UpdateInfo = info;
            }
        }

        public string Title => "發現新版本";
    }
}