using System;
using System.Collections.Generic;
using NAudio.Midi;

namespace SkyscapeMidiDisplayer.Services;

public class MidiInputService : IDisposable
{
    private MidiIn? _midiIn;
    private bool _disposed;
    
    public event EventHandler<MidiNoteEventArgs>? NoteOnReceived;
    public event EventHandler<MidiNoteEventArgs>? NoteOffReceived;
    public event EventHandler<MidiControlChangeEventArgs>? ControlChangeReceived;

    public bool IsConnected => _midiIn != null;
    public string? CurrentDeviceName { get; private set; }

    public static List<string> GetAvailableDevices()
    {
        var devices = new List<string>();
        for (int i = 0; i < MidiIn.NumberOfDevices; i++)
        {
            devices.Add(MidiIn.DeviceInfo(i).ProductName);
        }
        return devices;
    }

    public bool Connect(int deviceIndex)
    {
        Disconnect();

        if (deviceIndex < 0 || deviceIndex >= MidiIn.NumberOfDevices)
        {
            return false;
        }

        try
        {
            _midiIn = new MidiIn(deviceIndex);
            _midiIn.MessageReceived += OnMessageReceived;
            _midiIn.ErrorReceived += OnErrorReceived;
            _midiIn.Start();
            CurrentDeviceName = MidiIn.DeviceInfo(deviceIndex).ProductName;
            return true;
        }
        catch
        {
            _midiIn?.Dispose();
            _midiIn = null;
            return false;
        }
    }

    public bool Connect(string deviceName)
    {
        for (int i = 0; i < MidiIn.NumberOfDevices; i++)
        {
            if (MidiIn.DeviceInfo(i).ProductName == deviceName)
            {
                return Connect(i);
            }
        }
        return false;
    }

    public void Disconnect()
    {
        if (_midiIn != null)
        {
            try
            {
                _midiIn.Stop();
                _midiIn.MessageReceived -= OnMessageReceived;
                _midiIn.ErrorReceived -= OnErrorReceived;
                _midiIn.Dispose();
            }
            catch { }
            _midiIn = null;
            CurrentDeviceName = null;
        }
    }

    private void OnMessageReceived(object? sender, MidiInMessageEventArgs e)
    {
        int rawMessage = e.RawMessage;
        
        int message = rawMessage & 0xF0;
        int channel = rawMessage & 0x0F;
        int data1 = (rawMessage >> 8) & 0xFF;
        int data2 = (rawMessage >> 16) & 0xFF;

        switch (message)
        {
            case 0x90: // Note On
                if (data2 > 0)
                {
                    NoteOnReceived?.Invoke(this, new MidiNoteEventArgs(data1, data2));
                }
                else
                {
                    NoteOffReceived?.Invoke(this, new MidiNoteEventArgs(data1, 0));
                }
                break;
            case 0x80: // Note Off
                NoteOffReceived?.Invoke(this, new MidiNoteEventArgs(data1, 0));
                break;
            case 0xB0: // Control Change
                ControlChangeReceived?.Invoke(this, new MidiControlChangeEventArgs(data1, data2));
                break;
        }
    }

    private void OnErrorReceived(object? sender, MidiInMessageEventArgs e)
    {
        // 忽略错误事件
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        Disconnect();
    }
}

public class MidiNoteEventArgs : EventArgs
{
    public int NoteNumber { get; }
    public int Velocity { get; }

    public MidiNoteEventArgs(int noteNumber, int velocity)
    {
        NoteNumber = noteNumber;
        Velocity = velocity;
    }
}

public class MidiControlChangeEventArgs : EventArgs
{
    public int Controller { get; }
    public int Value { get; }

    public MidiControlChangeEventArgs(int controller, int value)
    {
        Controller = controller;
        Value = value;
    }
}