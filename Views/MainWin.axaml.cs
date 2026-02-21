using Avalonia.Controls;
using OB.ViewModels;
using System;

namespace OB.Views
{
    public partial class MainWin : Window
    {
        public MainWin()
        {
            InitializeComponent();
        }

        protected override void OnOpened(EventArgs e)
        {
            base.OnOpened(e);

            if (DataContext is MainViewModel vm)
            {
                _ = vm.DefaultNavigateAsync(); 
            }
        }
    }
}