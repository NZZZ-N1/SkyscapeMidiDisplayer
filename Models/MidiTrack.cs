using System.Collections.Generic;
using Avalonia.Media;

namespace SkyscapeMidiDisplayer.Models;

public class MidiTrack
{
    public int Index { get; set; }
    public string Name { get; set; } = string.Empty;
    public List<MidiNote> Notes { get; set; } = new();
    public IBrush Color { get; set; } = Brushes.DodgerBlue;
}
