using Avalonia.Controls;
using SkyscapeMidiDisplayer.ViewModels;
using SkyscapeMidiDisplayer.Services;

namespace SkyscapeMidiDisplayer
{
    public partial class SettingsWindow : Window
    {
        public SettingsWindow()
        {
            InitializeComponent();
            DataContext = new SettingsViewModel(this);
        }

        public SettingsWindow(MidiInputService midiInputService) : this()
        {
            DataContext = new SettingsViewModel(this, midiInputService);
        }
    }
}