using Avalonia.Media;

namespace SkyscapeMidiDisplayer.Models;

public enum HandType
{
    Left,
    Right,
    Unknown
}

public class MidiNote
{
    public int NoteNumber { get; set; }
    public string NoteName { get; set; } = string.Empty;
    public double StartTimeMs { get; set; }
    public double EndTimeMs { get; set; }
    public double DurationMs => EndTimeMs - StartTimeMs;
    public int Velocity { get; set; }
    public int Channel { get; set; }
    public int TrackIndex { get; set; }
    public IBrush? Color { get; set; }
    public HandType Hand { get; set; } = HandType.Unknown;

    public int Octave => NoteNumber / 12 - 1;
    public string NoteNameWithOctave => $"{NoteName}{Octave}";
}
