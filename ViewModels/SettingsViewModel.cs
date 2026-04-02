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

    [ObservableProperty] private bool _showWatermark = true;
    [ObservableProperty] private string _currentSoundFont = "钢琴";
    [ObservableProperty] private List<string> _availableSoundFonts = new();

    public SettingsViewModel(Window window)
    {
        _window = window;
        _settingsService = new SettingsService();
        LoadSettings();
        LoadAvailableSoundFonts();
    }

    private void LoadSettings()
    {
        var settings = _settingsService.LoadSettings();
        ShowWatermark = settings.ShowWatermark;
        CurrentSoundFont = settings.CurrentSoundFont;
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
            CurrentSoundFont = CurrentSoundFont
        };

        _settingsService.SaveSettings(settings);
        _window.Close();
    }

    [RelayCommand]
    private void Cancel()
    {
        _window.Close();
    }
}