using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Timers;
using System.Threading.Tasks;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Platform.Storage;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NAudio.Midi;
using SkyscapeMidiDisplayer.Models;
using SkyscapeMidiDisplayer.Services;

namespace SkyscapeMidiDisplayer.ViewModels;

public partial class MainViewModel : ViewModelBase
{
    private readonly MidiParserService _midiParser;
    private readonly AudioService _audioService;
    private readonly MidiInputService _midiInputService;
    private readonly System.Timers.Timer _playbackTimer;
    private readonly System.Timers.Timer _debounceTimer;
    private readonly Stopwatch _stopwatch;
    private MidiFileData? _currentMidiFile;
    private double _pausedPosition;
    private List<MidiNote> _sortedNotes = new();
    private int _currentNoteIndex;
    private double _lastPlayedTime;
    private double _debounceTargetTime;
    private bool _wasPlayingBeforeSeek;

    public AudioService AudioService => _audioService;

    [ObservableProperty]  private string _title = "Skyscape MIDI Displayer";
    [ObservableProperty] private string _statusMessage = "请选择一个MIDI文件开始";
    [ObservableProperty] private bool _isPlaying;
    [ObservableProperty] private bool _isFileLoaded;
    [ObservableProperty] private double _currentTimeMs;
    [ObservableProperty] private double _durationMs;
    [ObservableProperty] private double _speed = 200.0;
    [ObservableProperty] private double _playbackSpeed = 1.0;
    [ObservableProperty] private int _minNote = 21;
    [ObservableProperty] private int _maxNote = 108;
    [ObservableProperty] private string _fileName = "";
    [ObservableProperty] private int _totalNotes;
    [ObservableProperty] private int _trackCount;
    [ObservableProperty] private bool _isAudioEnabled = true;
    [ObservableProperty] private double _volume = 0.8;
    [ObservableProperty] private bool _showWatermark = true;
    [ObservableProperty] private string _blackKeyColor = "黑色";
    [ObservableProperty] private bool _isMidiInputMode;
    [ObservableProperty] private bool _isMidiInputConnected;
    [ObservableProperty] private string? _selectedMidiInputDevice;
    [ObservableProperty] private string _midiInputStatus = "未连接";

    public ObservableCollection<MidiNote> Notes { get; } = new();
    public ObservableCollection<MidiTrack> Tracks { get; } = new();
    public ObservableCollection<string> AvailableMidiInputDevices { get; } = new();

    public double Progress
    {
        get => DurationMs > 0 ? CurrentTimeMs / DurationMs : 0;
        set
        {
            if (DurationMs > 0)
            {
                var newTime = value * DurationMs;
                
                // 保存拖动前的播放状态
                _wasPlayingBeforeSeek = IsPlaying;
                
                // 如果正在播放，暂停播放
                if (IsPlaying)
                {
                    _pausedPosition = newTime;
                    StopPlayback();
                }
                
                CurrentTimeMs = newTime;
                _pausedPosition = newTime;
                _debounceTargetTime = newTime;
                
                // 重置防抖定时器
                _debounceTimer.Stop();
                _debounceTimer.Start();
                
                OnPropertyChanged();
            }
        }
    }

    private void OnDebounceTimerElapsed(object? sender, ElapsedEventArgs e)
    {
        // 在后台线程执行停止音符和重置索引的操作
        Task.Run(() =>
        {
            _audioService.StopAllNotes();
            ResetNoteIndex(_debounceTargetTime);
            
            // 如果拖动前正在播放，恢复播放
            if (_wasPlayingBeforeSeek)
            {
                Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                {
                    Play();
                });
            }
        });
    }

    public MainViewModel()
        {
            _midiParser = new MidiParserService();
            _audioService = new AudioService();
            _midiInputService = new MidiInputService();
            _stopwatch = new Stopwatch();
            _playbackTimer = new System.Timers.Timer(16);
            _playbackTimer.Elapsed += OnPlaybackTimerElapsed;
            
            _debounceTimer = new System.Timers.Timer(50);
            _debounceTimer.AutoReset = false;
            _debounceTimer.Elapsed += OnDebounceTimerElapsed;
            
            _midiInputService.NoteOnReceived += OnMidiInputNoteOn;
            _midiInputService.NoteOffReceived += OnMidiInputNoteOff;
            _midiInputService.ControlChangeReceived += OnMidiInputControlChange;
            
            RefreshAvailableMidiInputDevices();
            
            var settingsService = new SettingsService();
            var settings = settingsService.LoadSettings();
            ShowWatermark = settings.ShowWatermark;
            _audioService.SetSoundFont(settings.CurrentSoundFont);
            BlackKeyColor = settings.BlackKeyColor;
        }

    [RelayCommand]
    private async Task OpenFile(Window window)
    {
        var storageProvider = window.StorageProvider;
        var files = await storageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "选择MIDI文件",
            AllowMultiple = false,
            FileTypeFilter = new[]
            {
                new FilePickerFileType("MIDI Files") { Patterns = new[] { "*.mid", "*.midi" } },
                new FilePickerFileType("All Files") { Patterns = new[] { "*.*" } }
            }
        });

        if (files.Count > 0)
        {
            await LoadMidiFile(files[0].Path.LocalPath);
        }
    }

    private async Task LoadMidiFile(string filePath)
    {
        StopPlayback();

        await Task.Run(() =>
        {
            _currentMidiFile = _midiParser.ParseMidiFile(filePath);
        });

        if (_currentMidiFile == null)
        {
            StatusMessage = "无法加载MIDI文件";
            return;
        }

        Notes.Clear();
        Tracks.Clear();

        foreach (var track in _currentMidiFile.Tracks)
        {
            Tracks.Add(track);
            foreach (var note in track.Notes)
            {
                Notes.Add(note);
            }
        }

        _sortedNotes = Notes.OrderBy(n => n.StartTimeMs).ToList();

        DurationMs = _currentMidiFile.DurationMs;
        MinNote = _currentMidiFile.MinNote;
        MaxNote = _currentMidiFile.MaxNote;
        FileName = _currentMidiFile.FileName;
        TotalNotes = _currentMidiFile.TotalNoteCount;
        TrackCount = _currentMidiFile.Tracks.Count;

        CurrentTimeMs = 0;
        _pausedPosition = 0;
        _lastPlayedTime = 0;
        _currentNoteIndex = 0;
        IsFileLoaded = true;
        StatusMessage = $"已加载: {FileName} ({TotalNotes} 个音符, {TrackCount} 个轨道)";
    }

    [RelayCommand]
    private void Play()
    {
        if (!IsFileLoaded || IsPlaying) return;

        IsPlaying = true;
        _stopwatch.Restart();
        _playbackTimer.Start();
        StatusMessage = "正在播放...";
    }

    [RelayCommand]
    private void Pause()
    {
        if (!IsPlaying) return;

        _pausedPosition = CurrentTimeMs;
        _audioService.StopAllNotes();
        StopPlayback();
        StatusMessage = "已暂停";
    }

    [RelayCommand]
    private void Stop()
    {
        StopPlayback();
        _audioService.StopAllNotes();
        CurrentTimeMs = 0;
        _pausedPosition = 0;
        _currentNoteIndex = 0;
        _lastPlayedTime = 0;
        StatusMessage = "已停止";
    }

    private void StopPlayback()
    {
        _playbackTimer.Stop();
        _stopwatch.Stop();
        IsPlaying = false;
    }

    [RelayCommand]
    private void Restart()
    {
        _audioService.StopAllNotes();
        CurrentTimeMs = 0;
        _pausedPosition = 0;
        _currentNoteIndex = 0;
        _lastPlayedTime = 0;
        if (IsPlaying)
        {
            _stopwatch.Restart();
        }
    }

    [RelayCommand]
    private async Task OpenSettings(Window window)
    {
        var settingsWindow = new SettingsWindow(_midiInputService);
        await settingsWindow.ShowDialog(window);
        
        var settingsService = new SettingsService();
        var settings = settingsService.LoadSettings();
        ShowWatermark = settings.ShowWatermark;
        _audioService.SetSoundFont(settings.CurrentSoundFont);
        BlackKeyColor = settings.BlackKeyColor;
    }

    private void OnPlaybackTimerElapsed(object? sender, ElapsedEventArgs e)
    {
        if (_currentMidiFile == null) return;

        var newPosition = _pausedPosition + _stopwatch.Elapsed.TotalMilliseconds * PlaybackSpeed;

        if (newPosition >= DurationMs)
        {
            Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            {
                Stop();
                StatusMessage = "播放完成";
            });
            return;
        }

        if (IsAudioEnabled)
        {
            PlayNotesInRange(_lastPlayedTime, newPosition);
        }

        _lastPlayedTime = newPosition;

        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
        {
            CurrentTimeMs = newPosition;
            OnPropertyChanged(nameof(Progress));
        });
    }

    private void ResetNoteIndex(double time)
    {
        _currentNoteIndex = BinarySearchNoteIndex(time);
        _lastPlayedTime = time;
    }

    private int BinarySearchNoteIndex(double time)
    {
        if (_sortedNotes.Count == 0)
            return 0;

        int left = 0;
        int right = _sortedNotes.Count - 1;
        int result = _sortedNotes.Count;

        while (left <= right)
        {
            int mid = left + (right - left) / 2;
            if (_sortedNotes[mid].StartTimeMs >= time)
            {
                result = mid;
                right = mid - 1;
            }
            else
            {
                left = mid + 1;
            }
        }

        return result;
    }

    private void PlayNotesInRange(double startTime, double endTime)
    {
        while (_currentNoteIndex < _sortedNotes.Count)
        {
            var note = _sortedNotes[_currentNoteIndex];
            
            if (note.StartTimeMs >= endTime)
                break;

            if (note.StartTimeMs >= startTime)
            {
                _audioService.PlayNote(note.NoteNumber, note.Velocity, note.DurationMs);
            }

            _currentNoteIndex++;
        }
    }

    partial void OnPlaybackSpeedChanged(double value)
    {
        if (IsPlaying)
        {
            _pausedPosition = CurrentTimeMs;
            _stopwatch.Restart();
        }
    }

    partial void OnIsAudioEnabledChanged(bool value)
    {
        if (!value)
        {
            _audioService.StopAllNotes();
        }
    }

    partial void OnVolumeChanged(double value)
    {
        _audioService.Volume = (float)value;
    }

    partial void OnIsMidiInputModeChanged(bool value)
    {
        if (value && !IsMidiInputConnected && SelectedMidiInputDevice != null)
        {
            ConnectMidiInput();
        }
        else if (!value && IsMidiInputConnected)
        {
            DisconnectMidiInput();
        }
        StatusMessage = value ? $"MIDI输入模式: {_midiInputService.CurrentDeviceName ?? "未连接"}" : "已退出MIDI输入模式";
    }

    partial void OnSelectedMidiInputDeviceChanged(string? value)
    {
        if (IsMidiInputConnected)
        {
            DisconnectMidiInput();
            if (value != null && IsMidiInputMode)
            {
                ConnectMidiInput();
            }
        }
    }

    [RelayCommand]
    private void RefreshMidiInputDevices()
    {
        RefreshAvailableMidiInputDevices();
    }

    private void RefreshAvailableMidiInputDevices()
    {
        AvailableMidiInputDevices.Clear();
        var devices = MidiInputService.GetAvailableDevices();
        foreach (var device in devices)
        {
            AvailableMidiInputDevices.Add(device);
        }
    }

    [RelayCommand]
    private void ConnectMidiInput()
    {
        if (SelectedMidiInputDevice == null) return;

        if (_midiInputService.Connect(SelectedMidiInputDevice))
        {
            IsMidiInputConnected = true;
            MidiInputStatus = $"已连接: {SelectedMidiInputDevice}";
            StatusMessage = $"MIDI输入已连接: {SelectedMidiInputDevice}";
        }
        else
        {
            IsMidiInputConnected = false;
            MidiInputStatus = "连接失败";
            StatusMessage = "MIDI输入连接失败";
        }
    }

    [RelayCommand]
    private void DisconnectMidiInput()
    {
        _midiInputService.Disconnect();
        IsMidiInputConnected = false;
        MidiInputStatus = "未连接";
        StatusMessage = "MIDI输入已断开";
    }

    private void OnMidiInputNoteOn(object? sender, MidiNoteEventArgs e)
    {
        if (!IsMidiInputMode) return;
        
        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
        {
            // 使用长duration，让音符自然衰减
            // PianoNoteProvider 会在约200ms后进入持续阶段并自然衰减
            _audioService.PlayNote(e.NoteNumber, e.Velocity, 5000);
        });
    }

    private void OnMidiInputNoteOff(object? sender, MidiNoteEventArgs e)
    {
        if (!IsMidiInputMode) return;
        
        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
        {
            // 立即停止该音符，实现真实钢琴的"按键放开即停止"效果
            _audioService.StopNote(e.NoteNumber);
        });
    }

    private void OnMidiInputControlChange(object? sender, MidiControlChangeEventArgs e)
    {
        if (!IsMidiInputMode) return;
        
        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
        {
            // 延音踏板（Sustain Pedal）- 支持多种常见 CC 编号
            if (IsSustainPedalCC(e.Controller))
            {
                bool pressed = e.Value >= 64;
                StatusMessage = pressed ? "踏板: 踩下" : "踏板: 松开";
                _audioService.SetSustainPedal(pressed);
            }
            else if (e.Controller == 65) // Sostenuto Pedal
            {
                StatusMessage = e.Value >= 64 ? "延音踏板: 踩下" : "延音踏板: 松开";
            }
            else if (IsSoftPedalCC(e.Controller)) // Soft Pedal
            {
                bool pressed = e.Value >= 64;
                StatusMessage = pressed ? "柔音踏板: 踩下" : "柔音踏板: 松开";
                _audioService.SetSoftPedal(pressed);
            }
        });
    }

    private bool IsSustainPedalCC(int controller)
    {
        // 延音踏板相关的常见 CC 编号
        // CC 64: Damper Pedal (标准延音踏板)
        // CC 4: Foot Controller (脚踏控制器)
        // CC 11: Expression Controller (表情控制器，某些脚踏板使用)
        // CC 67: Soft Pedal (某些 Yamaha 键盘)
        // CC 91: Effect 1 Depth (某些键盘的延音踏板)
        // CC 92: Effect 2 Depth (某些键盘的延音踏板)
        // CC 93: Effect 3 Depth (某些键盘的延音踏板)
        // CC 94: Effect 4 Depth (某些键盘的延音踏板)
        // CC 12: Effect Control 1 (某些脚踏板)
        // CC 13: Effect Control 2 (某些脚踏板)
        // CC 14: Aux Control 1 (某些脚踏板)
        // CC 15: Aux Control 2 (某些脚踏板)
        // CC 84: Portamento Control (某些脚踏板)
        // CC 88: High Resolution Velocity Prefix (某些设备)
        return controller is 64 or 4 or 11 or 67 or 91 or 92 or 93 or 94 or 12 or 13 or 14 or 15 or 84 or 88;
    }

    private bool IsSoftPedalCC(int controller)
    {
        // 柔音踏板相关的常见 CC 编号
        // CC 67: Soft Pedal (标准)
        // CC 66: Sostenuto Pedal 兼 Soft Pedal (某些键盘)
        // CC 71: Sound Controller 1 (Brightness) - 某些设备
        // CC 72: Sound Controller 2 (Harmonic Intensity) - 某些设备
        // CC 73: Sound Controller 3 (Release Time) - 某些设备
        // CC 74: Sound Controller 4 (Attack Time) - 某些设备
        return controller is 67 or 66 or 71 or 72 or 73 or 74;
    }
}
