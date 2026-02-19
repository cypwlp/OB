using Prism.Commands;
using Prism.Dialogs;
using Prism.Mvvm;
using System;

namespace OB.ViewModels.Dialogs
{
    public class LoginViewModel : BindableBase, IDialogAware
    {
        private string _userName;
        private string _password;

        public string UserName
        {
            get => _userName;
            set => SetProperty(ref _userName, value);
        }

        public string Password
        {
            get => _password;
            set => SetProperty(ref _password, value);
        }

        public DialogCloseListener RequestClose { get; private set; }

        public string Title => "用户登录";

        public bool CanCloseDialog() => true;

        public void OnDialogClosed() { }

        public void OnDialogOpened(IDialogParameters parameters) { }

        // 新增事件，用于手动显示窗口时通知外部
        public event EventHandler<ButtonResult> LoginClosed;

        private bool CanLogin() => !string.IsNullOrWhiteSpace(UserName) && !string.IsNullOrWhiteSpace(Password);

        public DelegateCommand LoginCommand => new DelegateCommand(Login, CanLogin)
            .ObservesProperty(() => UserName)
            .ObservesProperty(() => Password);

        private void Login()
        {
            // 替换为实际的验证逻辑
            if (UserName == "admin" && Password == "admin")
            {
                // 触发事件（手动模式）
                LoginClosed?.Invoke(this, ButtonResult.OK);
                // 触发 Prism 对话框关闭（自动模式）
                RequestClose.Invoke(new DialogParameters(), ButtonResult.OK);
            }
            else
            {
                // 登录失败，可显示错误提示（此处略）
            }
        }

        public DelegateCommand CancelCommand => new DelegateCommand(Cancel);

        private void Cancel()
        {
            LoginClosed?.Invoke(this, ButtonResult.Cancel);
            RequestClose.Invoke(null, ButtonResult.Cancel);
        }
    }
}