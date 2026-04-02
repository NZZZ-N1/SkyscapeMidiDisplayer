using Avalonia.Controls;
using SkyscapeMidiDisplayer.ViewModels;

namespace SkyscapeMidiDisplayer
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            DataContext = new MainViewModel();
        }
    }
}
