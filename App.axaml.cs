using System.IO;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using EasySaving;
using SkyscapeMidiDisplayer.Converters;

namespace SkyscapeMidiDisplayer
{
    public partial class App : Application
    {
        public override void Initialize()
        {
            AvaloniaXamlLoader.Load(this);
            
            // 初始化EasySaving，设置文件保存在程序根目录下的Settings文件夹中
            var settingsFolder = Path.Combine(SavingInfo.ExecuteAppDirection, "Settings");
            new SavingInfo(settingsFolder, "dta", true);
        }

        public override void OnFrameworkInitializationCompleted()
        {
            if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                desktop.MainWindow = new MainWindow();
            }

            base.OnFrameworkInitializationCompleted();
        }
    }
}
