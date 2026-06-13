using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;
using NAudio.Midi;
using SkyscapeMidiDisplayer.Services.SoundFonts;

namespace SkyscapeMidiDisplayer.Services;

public class AudioService : IDisposable
{
    private readonly WaveOutEvent _waveOut;
    private readonly MixingSampleProvider _mixer;
    private readonly VolumeSampleProvider _volumeProvider;
    private readonly ConcurrentBag<ISampleProvider> _activeNotes = new();
    private readonly ConcurrentDictionary<int, ISampleProvider> _activeNotesByKey = new();
    private readonly ConcurrentQueue<int> _sustainedNotes = new(); // 延音踏板保持的音符队列
    private bool _disposed;
    private MidiOut? _midiOut;
    private bool _useMidiSynth;
    private readonly SoundFontManager _soundFontManager;
    private bool _sustainPedalPressed; // 延音踏板状态
    private bool _softPedalPressed; // 柔音踏板状态
    private double _softPedalVolume = 1.0; // 柔音踏板音量系数

    private static readonly double[] NoteFrequencies = GenerateNoteFrequencies();

    private static double[] GenerateNoteFrequencies()
    {
        var frequencies = new double[128];
        for (int i = 0; i < 128; i++)
        {
            frequencies[i] = 440.0 * Math.Pow(2, (i - 69) / 12.0);
        }
        return frequencies;
    }

    public AudioService()
    {
        _mixer = new MixingSampleProvider(WaveFormat.CreateIeeeFloatWaveFormat(48000, 2));
        _mixer.ReadFully = true;

        _volumeProvider = new VolumeSampleProvider(_mixer)
        {
            Volume = 0.8f
        };

        _waveOut = new WaveOutEvent
        {
            DesiredLatency = 50,
            NumberOfBuffers = 4
        };
        _waveOut.Init(_volumeProvider);
        _waveOut.Play();

        _soundFontManager = new SoundFontManager();

        TryInitializeMidiSynth();
    }

    public string CurrentSoundFont => _soundFontManager.CurrentSoundFontName;

    public void SetSoundFont(string name)
    {
        _soundFontManager.SetCurrentSoundFontByName(name);
    }

    public System.Collections.Generic.List<ISoundFont> AvailableSoundFonts => _soundFontManager.AvailableSoundFonts;

    private void TryInitializeMidiSynth()
    {
        _midiOut = null;
        _useMidiSynth = false;
    }

    public bool IsUsingMidiSynth => _useMidiSynth;

    public float Volume
    {
        get => _volumeProvider.Volume;
        set => _volumeProvider.Volume = Math.Clamp(value, 0f, 1f);
    }

    public void SetSustainPedal(bool pressed)
    {
        _sustainPedalPressed = pressed;
        
        if (!pressed)
        {
            // 松开延音踏板，停止所有被保持的音符
            while (_sustainedNotes.TryDequeue(out var noteNumber))
            {
                StopNoteInternal(noteNumber);
            }
        }
    }

    public void SetSoftPedal(bool pressed)
    {
        _softPedalPressed = pressed;
        // 柔音踏板效果：降低音量并改变音色
        _softPedalVolume = pressed ? 0.6 : 1.0;
    }

    public void PlayNote(int noteNumber, int velocity, double durationMs)
    {
        if (_disposed) return;

        // 如果该音符正在播放，先停止它（带强制清理）
        StopNote(noteNumber, true);

        if (_useMidiSynth && _midiOut != null)
        {
            PlayMidiNote(noteNumber, velocity, durationMs);
        }
        else
        {
            PlaySynthesizedNote(noteNumber, velocity, durationMs);
        }
    }

    public void StopNote(int noteNumber)
    {
        StopNote(noteNumber, false);
    }

    public void StopNote(int noteNumber, bool forceRemove)
    {
        // 如果延音踏板正在踩下，且不是强制移除，将音符添加到保持队列
        if (_sustainPedalPressed && !forceRemove)
        {
            // 将音符加入延音队列，但不从当前播放中移除
            // 音符会继续播放直到踏板松开
            _sustainedNotes.Enqueue(noteNumber);
            return;
        }

        StopNoteInternal(noteNumber, forceRemove);
    }

    private void StopNoteInternal(int noteNumber)
    {
        StopNoteInternal(noteNumber, false);
    }

    private void StopNoteInternal(int noteNumber, bool forceRemove)
    {
        // 停止合成音符
        if (_activeNotesByKey.TryGetValue(noteNumber, out var provider))
        {
            var stopMethod = provider.GetType().GetMethod("Stop");
            if (stopMethod != null)
            {
                stopMethod.Invoke(provider, null);
            }
            
            // 如果是强制移除（重新播放同一音符），立即清理
            if (forceRemove)
            {
                _activeNotesByKey.TryRemove(noteNumber, out _);
                _mixer.RemoveMixerInput(provider);
            }
            // 否则让释放效果播放完毕后自动清理
        }

        // 停止 MIDI 音符
        if (_midiOut != null)
        {
            try
            {
                _midiOut.Send(MidiMessage.StopNote(noteNumber, 0, 1).RawData);
            }
            catch { }
        }
    }

    private void PlayMidiNote(int noteNumber, int velocity, double durationMs)
    {
        try
        {
            _midiOut?.Send(MidiMessage.StartNote(noteNumber, velocity, 1).RawData);
            
            _ = System.Threading.Tasks.Task.Run(async () =>
            {
                await System.Threading.Tasks.Task.Delay(TimeSpan.FromMilliseconds(durationMs));
                try
                {
                    _midiOut?.Send(MidiMessage.StopNote(noteNumber, 0, 1).RawData);
                }
                catch { }
            });
        }
        catch
        {
            PlaySynthesizedNote(noteNumber, velocity, durationMs);
        }
    }

    private void PlaySynthesizedNote(int noteNumber, int velocity, double durationMs)
    {
        var frequency = NoteFrequencies[noteNumber];
        var volume = Math.Min(1.0, velocity / 127.0 * 0.4);

        var actualDuration = Math.Max(durationMs, 200.0);
        var provider = _soundFontManager.CurrentSoundFont.CreateNoteProvider(frequency, volume, actualDuration, noteNumber);
        
        _activeNotes.Add(provider);
        _activeNotesByKey[noteNumber] = provider;
        _mixer.AddMixerInput(provider);

        // 使用反射检查提供者是否有Finished事件
        var finishedEvent = provider.GetType().GetEvent("Finished");
        if (finishedEvent != null)
        {
            finishedEvent.AddEventHandler(provider, new EventHandler((s, e) =>
            {
                _activeNotes.TryTake(out _);
                _activeNotesByKey.TryRemove(noteNumber, out _);
                // 注意：不要在这里调用 RemoveMixerInput
                // MixingSampleProvider 会在 Read 返回 0 时自动移除输入
                // 在 Read 方法内部触发事件并调用 RemoveMixerInput 会导致集合修改冲突
            }));
        }
    }

    public void StopAllNotes()
    {
        while (_activeNotes.TryTake(out var note))
        {
            // 使用反射调用Stop方法
            var stopMethod = note.GetType().GetMethod("Stop");
            if (stopMethod != null)
            {
                stopMethod.Invoke(note, null);
            }
        }

        if (_midiOut != null)
        {
            for (int i = 0; i < 128; i++)
            {
                try
                {
                    _midiOut.Send(MidiMessage.StopNote(i, 0, 1).RawData);
                }
                catch { }
            }
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        StopAllNotes();
        _waveOut?.Stop();
        _waveOut?.Dispose();
        _midiOut?.Dispose();
    }

    private class PianoNoteProvider : ISampleProvider
    {
        private readonly double _frequency;
        private readonly double _peakVolume;
        private readonly double _durationMs;
        private readonly int _noteNumber;
        private double _phase;
        private double _samplePosition;
        private bool _stopped;
        private bool _isReleasing;
        private double _releaseStartTimeMs;
        private double _releaseStartEnvelope;  // 保存释放开始时的包络值

        private double[] _partialPhases = Array.Empty<double>();
        private double[] _partialAmplitudes = Array.Empty<double>();
        private double[] _partialDecays = Array.Empty<double>();
        private double[] _partialFreqRatios = Array.Empty<double>();
        private double _noteRatio;

        private const double AttackMs = 3;
        private const double DecayMs = 200;
        private const double SustainLevel = 0.6;
        private const double ReleaseMs = 250;  // 增加释放时间使尾音更明显

        private static readonly Random _random = new();

        public event EventHandler? Finished;

        public WaveFormat WaveFormat { get; }

        public PianoNoteProvider(double frequency, double volume, double durationMs, int noteNumber)
        {
            _frequency = frequency;
            _peakVolume = volume;
            _durationMs = durationMs;
            _noteNumber = noteNumber;
            WaveFormat = WaveFormat.CreateIeeeFloatWaveFormat(44100, 2);
            InitializePartials();
        }

        public void Stop()
        {
            // 进入快速释放阶段，而不是立即停止
            if (!_isReleasing && !_stopped)
            {
                _isReleasing = true;
                _releaseStartTimeMs = _samplePosition / WaveFormat.SampleRate * 1000;
                // 保存释放开始时的包络值
                _releaseStartEnvelope = GetBaseEnvelope(_releaseStartTimeMs);
            }
        }

        private void InitializePartials()
        {
            _noteRatio = (_noteNumber - 21) / 87.0;
            _noteRatio = Math.Clamp(_noteRatio, 0, 1);

            // 使用更多泛音以获得更丰富的钢琴音色
            int numPartials = 16;

            _partialPhases = new double[numPartials];
            _partialAmplitudes = new double[numPartials];
            _partialDecays = new double[numPartials];
            _partialFreqRatios = new double[numPartials];

            // 钢琴的非谐性系数（基于琴弦物理特性）
            double inharmonicityB = 0.0003 * Math.Pow(_frequency / 100, 1.5);

            for (int i = 0; i < numPartials; i++)
            {
                double n = i + 1;
                
                // 非谐性频率计算
                double inharmonicFreq = n * Math.Sqrt(1 + inharmonicityB * n * n);
                _partialFreqRatios[i] = inharmonicFreq;
                
                // 振幅包络：低频泛音更强，高频泛音更弱
                // 使用更陡峭的衰减来模拟钢琴音色
                double amplitudeDecay = Math.Pow(n, -1.5);
                
                // 根据音高调整音色：低音更浑厚，高音更明亮
                if (_noteRatio < 0.3) // 低音区
                {
                    amplitudeDecay *= (1.0 + 0.5 / n); // 增强低频泛音
                }
                else if (_noteRatio > 0.7) // 高音区
                {
                    amplitudeDecay *= (1.0 - 0.3 * (_noteRatio - 0.7)); // 稍微减弱高频
                }
                
                _partialAmplitudes[i] = amplitudeDecay;
                
                // 衰减率：高频泛音衰减更快
                _partialDecays[i] = 0.5 + n * 0.4 + n * n * 0.05;
                
                // 随机相位避免相位抵消
                _partialPhases[i] = _random.NextDouble() * 2 * Math.PI;
            }
        }

        public int Read(float[] buffer, int offset, int sampleCount)
        {
            if (_stopped)
            {
                Array.Clear(buffer, offset, sampleCount);
                return 0;
            }

            int samplesRead = 0;
            double sampleRate = WaveFormat.SampleRate;
            int channels = WaveFormat.Channels;

            for (int i = 0; i < sampleCount && !_stopped; i += channels)
            {
                double timeMs = _samplePosition / sampleRate * 1000;

                // 检查快速释放是否完成
                if (_isReleasing)
                {
                    double releaseProgress = (timeMs - _releaseStartTimeMs) / ReleaseMs;
                    if (releaseProgress >= 1.0)
                    {
                        _stopped = true;
                        break;
                    }
                }
                // 检查是否超过持续时间
                else if (timeMs >= _durationMs)
                {
                    _stopped = true;
                    break;
                }

                double sample = GeneratePianoSample(timeMs);
                double envelope = CalculateEnvelope(timeMs);

                float sampleValue = (float)(sample * envelope * _peakVolume);

                for (int c = 0; c < channels && i + c < sampleCount; c++)
                {
                    buffer[offset + i + c] = sampleValue;
                }

                _phase += _frequency / sampleRate;
                _samplePosition++;
                samplesRead += channels;
            }

            for (int i = samplesRead; i < sampleCount; i++)
            {
                buffer[offset + i] = 0;
            }

            if (_stopped)
            {
                Finished?.Invoke(this, EventArgs.Empty);
            }

            return samplesRead;
        }

        private double GeneratePianoSample(double timeMs)
        {
            double sample = 0;
            double timeSec = timeMs / 1000.0;

            for (int i = 0; i < _partialPhases.Length; i++)
            {
                double partialFreq = _frequency * _partialFreqRatios[i];
                double phase = _partialPhases[i] + partialFreq * timeSec * 2 * Math.PI;
                double amplitude = _partialAmplitudes[i] * Math.Exp(-_partialDecays[i] * timeSec);
                sample += Math.Sin(phase) * amplitude;
            }

            // 琴槌敲击噪声 - 模拟琴槌击弦的机械噪声
            double hammerNoise = 0;
            if (timeMs < 8)
            {
                // 噪声随时间快速衰减
                double noiseDecay = Math.Exp(-timeMs / 2.0);
                // 高频噪声更多
                double noiseAmount = noiseDecay * 0.12 * (1.0 + 0.5 * _noteRatio);
                hammerNoise = (_random.NextDouble() * 2 - 1) * noiseAmount;
            }

            // 归一化并添加噪声
            sample = sample / _partialPhases.Length * 2.5 + hammerNoise;
            return Math.Clamp(sample, -1.0, 1.0);
        }

        private double CalculateEnvelope(double timeMs)
        {
            // 手动触发的快速释放阶段
            if (_isReleasing)
            {
                double releaseProgress = (timeMs - _releaseStartTimeMs) / ReleaseMs;
                releaseProgress = Math.Min(1.0, releaseProgress);
                
                // 使用释放开始时的包络值进行衰减，而不是当前时间的包络值
                // 使用更缓的衰减曲线，使尾音更明显
                double decay = Math.Pow(1 - releaseProgress, 2); // 二次曲线衰减
                return _releaseStartEnvelope * decay;
            }

            double releaseStart = _durationMs - ReleaseMs;

            if (releaseStart < AttackMs + DecayMs)
            {
                releaseStart = Math.Max(AttackMs + DecayMs * 0.3, _durationMs * 0.6);
            }

            // 自动释放阶段（duration结束前）
            if (timeMs >= releaseStart)
            {
                double releaseProgress = (timeMs - releaseStart) / (_durationMs - releaseStart);
                releaseProgress = Math.Min(1.0, releaseProgress);
                // 指数衰减释放
                return SustainLevel * Math.Exp(-5 * releaseProgress);
            }

            // 起音阶段 - 快速上升到峰值
            if (timeMs < AttackMs)
            {
                return Math.Pow(timeMs / AttackMs, 0.3);
            }

            // 衰减阶段 - 从峰值衰减到持续电平
            if (timeMs < AttackMs + DecayMs)
            {
                double decayProgress = (timeMs - AttackMs) / DecayMs;
                return 1.0 - (1.0 - SustainLevel) * decayProgress;
            }

            // 持续阶段
            return SustainLevel;
        }

        private double GetBaseEnvelope(double timeMs)
        {
            // 计算正常的包络值（不考虑释放）
            // 起音阶段
            if (timeMs < AttackMs)
            {
                return Math.Pow(timeMs / AttackMs, 0.3);
            }

            // 衰减阶段
            if (timeMs < AttackMs + DecayMs)
            {
                double decayProgress = (timeMs - AttackMs) / DecayMs;
                return 1.0 - (1.0 - SustainLevel) * decayProgress;
            }

            // 持续阶段
            return SustainLevel;
        }
    }
}
