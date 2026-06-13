namespace SkyscapeMidiDisplayer.Services;

public class AppSettings
{
    public double DefaultVolume { get; set; } = 0.8;
    public bool IsAudioEnabled { get; set; } = true;
    public double DefaultSpeed { get; set; } = 200.0;
    public double DefaultPlaybackSpeed { get; set; } = 1.0;
    public bool ShowWatermark { get; set; } = true;
    public string CurrentSoundFont { get; set; } = "钢琴";
    public string BlackKeyColor { get; set; } = "黑色";
}