using OB.Tools;
using Prism.Commands;
using Prism.Dialogs;
using Prism.Mvvm;
using System;
using System.Threading.Tasks;

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

        private bool CanLogin() => !string.IsNullOrWhiteSpace(UserName) && !string.IsNullOrWhiteSpace(Password);

        public DelegateCommand LoginCommand => new DelegateCommand(Login, CanLogin)
            .ObservesProperty(() => UserName)
            .ObservesProperty(() => Password);

        private async void Login()
        {
            var dbtools = new DBTools();
            bool success = await Task.Run(() => dbtools.Initialize(UserName, Password));
            if (success)
            {
                // 登录成功，关闭对话框并返回 OK 结果
                RequestClose.Invoke(new DialogParameters(), ButtonResult.OK);
            }
            else
            {
                // 登录失败：可以显示错误提示（可选）
                // 例如使用 IDialogService 显示一个消息框（需要注入 IDialogService）
                // 这里简单处理：不做任何操作，让用户继续输入
            }
        }

        public DelegateCommand CancelCommand => new DelegateCommand(Cancel);

        private void Cancel()
        {
            // 用户点击取消，关闭对话框并返回 Cancel 结果
            RequestClose.Invoke(null, ButtonResult.Cancel);
        }

        public DelegateCommand ShowSetCommand => new DelegateCommand(ShowSet);

        private void ShowSet()
        {
            // 如果需要打开设置窗口，可以在这里实现
            // 可以通过 RequestClose 触发一个自定义结果，然后在外部处理
            // 或者注入 IDialogService 来打开另一个对话框
        }
    }
}