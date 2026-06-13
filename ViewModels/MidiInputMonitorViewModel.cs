using System;
using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Avalonia.Controls;
using SkyscapeMidiDisplayer.Services;

namespace SkyscapeMidiDisplayer.ViewModels;

public partial class MidiInputMonitorViewModel : ViewModelBase, IDisposable
{
    private readonly MidiInputService _midiInputService;
    private readonly Window _window;
    private bool _disposed;

    [ObservableProperty] private bool _isConnected;
    [ObservableProperty] private string _connectionStatus = "未连接";
    [ObservableProperty] private string? _selectedDevice;
    [ObservableProperty] private bool _sustainPedalPressed;
    [ObservableProperty] private bool _sostenutoPedalPressed;
    [ObservableProperty] private bool _softPedalPressed;
    [ObservableProperty] private string _lastNoteInfo = "-";
    [ObservableProperty] private string _lastCCInfo = "-";

    public ObservableCollection<string> AvailableDevices { get; } = new();
    public ObservableCollection<int> ActiveNotes { get; } = new();

    public MidiInputMonitorViewModel(Window window, MidiInputService midiInputService)
    {
        _window = window;
        _midiInputService = midiInputService;
        
        _midiInputService.NoteOnReceived += OnNoteOn;
        _midiInputService.NoteOffReceived += OnNoteOff;
        _midiInputService.ControlChangeReceived += OnControlChange;
        
        RefreshDevices();
        
        IsConnected = _midiInputService.IsConnected;
        if (IsConnected)
        {
            ConnectionStatus = $"已连接: {_midiInputService.CurrentDeviceName}";
            SelectedDevice = _midiInputService.CurrentDeviceName;
        }
    }

    [RelayCommand]
    private void RefreshDevices()
    {
        AvailableDevices.Clear();
        foreach (var device in MidiInputService.GetAvailableDevices())
        {
            AvailableDevices.Add(device);
        }
    }

    [RelayCommand]
    private void Connect()
    {
        if (string.IsNullOrEmpty(SelectedDevice)) return;
        
        if (_midiInputService.Connect(SelectedDevice))
        {
            IsConnected = true;
            ConnectionStatus = $"已连接: {SelectedDevice}";
        }
        else
        {
            ConnectionStatus = "连接失败";
        }
    }

    [RelayCommand]
    private void Disconnect()
    {
        _midiInputService.Disconnect();
        IsConnected = false;
        ConnectionStatus = "未连接";
        ActiveNotes.Clear();
        SustainPedalPressed = false;
        SostenutoPedalPressed = false;
        SoftPedalPressed = false;
    }

    private void OnNoteOn(object? sender, MidiNoteEventArgs e)
    {
        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
        {
            if (!ActiveNotes.Contains(e.NoteNumber))
            {
                ActiveNotes.Add(e.NoteNumber);
            }
            LastNoteInfo = $"音符: {GetNoteName(e.NoteNumber)} (力度: {e.Velocity})";
        });
    }

    private void OnNoteOff(object? sender, MidiNoteEventArgs e)
    {
        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
        {
            ActiveNotes.Remove(e.NoteNumber);
        });
    }

    private void OnControlChange(object? sender, MidiControlChangeEventArgs e)
    {
        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
        {
            // 显示所有 CC 消息，便于调试
            LastCCInfo = $"CC {e.Controller} = {e.Value}";

            // 延音踏板（Sustain Pedal）- 支持多种常见 CC 编号
            // 参考标准 MIDI 规范和主流设备
            if (IsSustainPedalCC(e.Controller))
            {
                SustainPedalPressed = e.Value >= 64;
            }

            // 延音踏板（Sostenuto Pedal）- CC 65
            if (e.Controller == 65)
            {
                SostenutoPedalPressed = e.Value >= 64;
            }

            // 柔音踏板（Soft Pedal）- 支持多种 CC 编号
            if (IsSoftPedalCC(e.Controller))
            {
                SoftPedalPressed = e.Value >= 64;
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

    private static string GetNoteName(int noteNumber)
    {
        string[] noteNames = { "C", "C#", "D", "D#", "E", "F", "F#", "G", "G#", "A", "A#", "B" };
        int octave = (noteNumber / 12) - 1;
        int noteIndex = noteNumber % 12;
        return $"{noteNames[noteIndex]}{octave}";
    }

    [RelayCommand]
    private void Close()
    {
        _window.Close();
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        
        _midiInputService.NoteOnReceived -= OnNoteOn;
        _midiInputService.NoteOffReceived -= OnNoteOff;
        _midiInputService.ControlChangeReceived -= OnControlChange;
    }
}