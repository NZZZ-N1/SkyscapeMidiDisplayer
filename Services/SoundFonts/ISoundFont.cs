using NAudio.Wave;

namespace SkyscapeMidiDisplayer.Services.SoundFonts;

public interface ISoundFont
{
    string Name { get; }
    ISampleProvider CreateNoteProvider(double frequency, double volume, double durationMs, int noteNumber);
}
