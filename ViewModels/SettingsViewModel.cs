using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia.Controls;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SkyscapeMidiDisplayer.Services;
using SkyscapeMidiDisplayer.Services.SoundFonts;

namespace SkyscapeMidiDisplayer.ViewModels;

public partial class SettingsViewModel : ViewModelBase
{
    private readonly Window _window;
    private readonly SettingsService _settingsService;
    private readonly MidiInputService? _midiInputService;

    [ObservableProperty] private bool _showWatermark = true;
    [ObservableProperty] private string _currentSoundFont = "钢琴";
    [ObservableProperty] private List<string> _availableSoundFonts = new();
    [ObservableProperty] private string _blackKeyColor = "黑色";
    [ObservableProperty] private List<string> _availableBlackKeyColors = new() { "黑色", "粉色" };

    public SettingsViewModel(Window window) : this(window, null)
    {
    }

    public SettingsViewModel(Window window, MidiInputService? midiInputService)
    {
        _window = window;
        _settingsService = new SettingsService();
        _midiInputService = midiInputService;
        LoadSettings();
        LoadAvailableSoundFonts();
    }

    private void LoadSettings()
    {
        var settings = _settingsService.LoadSettings();
        ShowWatermark = settings.ShowWatermark;
        CurrentSoundFont = settings.CurrentSoundFont;
        BlackKeyColor = settings.BlackKeyColor;
    }

    private void LoadAvailableSoundFonts()
    {
        var soundFontManager = new SoundFontManager();
        AvailableSoundFonts = soundFontManager.AvailableSoundFonts.Select(sf => sf.Name).ToList();
    }

    [RelayCommand]
    private void Save()
    {
        var settings = new AppSettings
        {
            ShowWatermark = ShowWatermark,
            CurrentSoundFont = CurrentSoundFont,
            BlackKeyColor = BlackKeyColor
        };

        _settingsService.SaveSettings(settings);
        _window.Close();
    }

    [RelayCommand]
    private void Cancel()
    {
        _window.Close();
    }

    [RelayCommand]
    private void OpenMidiMonitor()
    {
        var monitorWindow = new MidiInputMonitorWindow(_midiInputService ?? new MidiInputService());
        monitorWindow.Show(_window);
    }
}