using System.Collections.Generic;
using System.Linq;

namespace SkyscapeMidiDisplayer.Models;

public class MidiFileData
{
    public string FilePath { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public List<MidiTrack> Tracks { get; set; } = new();
    public double DurationMs { get; set; }
    public int TicksPerQuarterNote { get; set; }
    public int MinNote { get; set; } = 21;
    public int MaxNote { get; set; } = 108;
    public int TotalNoteCount => Tracks.Sum(t => t.Notes.Count);
}
