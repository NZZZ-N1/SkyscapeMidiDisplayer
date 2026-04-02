using System.Collections.Generic;

namespace SkyscapeMidiDisplayer.Services.SoundFonts;

public class SoundFontManager
{
    private readonly List<ISoundFont> _soundFonts;
    private int _currentSoundFontIndex;

    public SoundFontManager()
    {
        _soundFonts = new List<ISoundFont>
        {
            new PianoSoundFont(),
            new EightBitSoundFont(),
            new GuitarSoundFont(),
            new PipaSoundFont(),
            new GuzhengSoundFont(),
            new SynthSoundFont()
        };
        _currentSoundFontIndex = 0;
    }

    public List<ISoundFont> AvailableSoundFonts => _soundFonts;

    public ISoundFont CurrentSoundFont => _soundFonts[_currentSoundFontIndex];

    public string CurrentSoundFontName => CurrentSoundFont.Name;

    public void SetCurrentSoundFontByName(string name)
    {
        int index = _soundFonts.FindIndex(sf => sf.Name == name);
        if (index >= 0)
        {
            _currentSoundFontIndex = index;
        }
    }

    public void SetCurrentSoundFontByIndex(int index)
    {
        if (index >= 0 && index < _soundFonts.Count)
        {
            _currentSoundFontIndex = index;
        }
    }
}
