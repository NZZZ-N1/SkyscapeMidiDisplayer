using EasySaving;

namespace SkyscapeMidiDisplayer.Services;

public class SettingsService
{
    private const string SettingsFileName = "AppSettings";

    public AppSettings LoadSettings()
    {
        var pack = DataSaving.Load(SettingsFileName, true);
        
        if (pack == null)
        {
            return new AppSettings();
        }

        return new AppSettings
        {
            DefaultVolume = pack.TryGetValue("DefaultVolume", 0.8),
            IsAudioEnabled = pack.TryGetValue("IsAudioEnabled", true),
            DefaultSpeed = pack.TryGetValue("DefaultSpeed", 200.0),
            DefaultPlaybackSpeed = pack.TryGetValue("DefaultPlaybackSpeed", 1.0),
            ShowWatermark = pack.TryGetValue("ShowWatermark", true),
            CurrentSoundFont = pack.TryGetValue("CurrentSoundFont", "钢琴"),
            BlackKeyColor = pack.TryGetValue("BlackKeyColor", "黑色")
        };
    }

    public void SaveSettings(AppSettings settings)
    {
        var pack = new SavingPack();
        pack.Add("DefaultVolume", settings.DefaultVolume);
        pack.Add("IsAudioEnabled", settings.IsAudioEnabled);
        pack.Add("DefaultSpeed", settings.DefaultSpeed);
        pack.Add("DefaultPlaybackSpeed", settings.DefaultPlaybackSpeed);
        pack.Add("ShowWatermark", settings.ShowWatermark);
        pack.Add("CurrentSoundFont", settings.CurrentSoundFont);
        pack.Add("BlackKeyColor", settings.BlackKeyColor);

        DataSaving.Save(pack, SettingsFileName);
    }
}
