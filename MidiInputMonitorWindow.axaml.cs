using Avalonia.Controls;
using SkyscapeMidiDisplayer.ViewModels;
using SkyscapeMidiDisplayer.Services;

namespace SkyscapeMidiDisplayer;

public partial class MidiInputMonitorWindow : Window
{
    public MidiInputMonitorWindow()
    {
        InitializeComponent();
    }

    public MidiInputMonitorWindow(MidiInputService midiInputService) : this()
    {
        DataContext = new MidiInputMonitorViewModel(this, midiInputService);
    }
}