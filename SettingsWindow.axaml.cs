using Avalonia.Controls;
using SkyscapeMidiDisplayer.ViewModels;

namespace SkyscapeMidiDisplayer
{
    public partial class SettingsWindow : Window
    {
        public SettingsWindow()
        {
            InitializeComponent();
            DataContext = new SettingsViewModel(this);
        }
    }
}