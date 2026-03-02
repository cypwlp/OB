using OB.Models;
using Prism.Dialogs;
using Prism.Mvvm;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OB.ViewModels.Dialogs
{
    public class SubclassViewModel : BindableBase, IDialogAware
    {
        public DialogCloseListener RequestClose  {get;set;}
        private YoloClassSetting _currentSetting;
        public YoloClassSetting CurrentSetting
        {
            get => _currentSetting;
            set => SetProperty(ref _currentSetting, value);
        }

        public bool CanCloseDialog()
        {
            return true;
        }
   
        public void OnDialogClosed()
        {
            
        }

        public void OnDialogOpened(IDialogParameters parameters)
        {
            parameters.TryGetValue("setting", out YoloClassSetting setting);
            CurrentSetting = setting;
        }
    }
}
