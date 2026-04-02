using System;
using NAudio.Wave;

namespace SkyscapeMidiDisplayer.Services.SoundFonts;

public class SynthSoundFont : ISoundFont
{
    public string Name => "电子合成";

    public ISampleProvider CreateNoteProvider(double frequency, double volume, double durationMs, int noteNumber)
    {
        return new SynthNoteProvider(frequency, volume, durationMs, noteNumber);
    }

    private class SynthNoteProvider : ISampleProvider
    {
        private readonly double _frequency;
        private readonly double _peakVolume;
        private readonly double _durationMs;
        private readonly int _noteNumber;
        private double _phase;
        private double _samplePosition;
        private bool _stopped;

        private double[] _partialPhases = Array.Empty<double>();
        private double[] _partialAmplitudes = Array.Empty<double>();
        private double[] _partialDecays = Array.Empty<double>();
        private double[] _partialFreqRatios = Array.Empty<double>();
        private double[] _partialModulation = Array.Empty<double>();
        private double _noteRatio;

        private const double AttackMs = 20;
        private const double DecayMs = 500;
        private const double SustainLevel = 0.7;
        private const double ReleaseMs = 1200;

        private static readonly Random _random = new();

        public event EventHandler? Finished;

        public WaveFormat WaveFormat { get; }

        public SynthNoteProvider(double frequency, double volume, double durationMs, int noteNumber)
        {
            _frequency = frequency;
            _peakVolume = volume;
            _durationMs = durationMs;
            _noteNumber = noteNumber;
            WaveFormat = NAudio.Wave.WaveFormat.CreateIeeeFloatWaveFormat(48000, 2);
            InitializePartials();
        }

        public void Stop()
        {
            _stopped = true;
            Finished?.Invoke(this, EventArgs.Empty);
        }

        private void InitializePartials()
        {
            _noteRatio = (_noteNumber - 21) / 87.0;
            _noteRatio = Math.Clamp(_noteRatio, 0, 1);

            // 电子合成音色的泛音结构
            int numPartials = 16;

            _partialPhases = new double[numPartials];
            _partialAmplitudes = new double[numPartials];
            _partialDecays = new double[numPartials];
            _partialFreqRatios = new double[numPartials];
            _partialModulation = new double[numPartials];

            for (int i = 0; i < numPartials; i++)
            {
                double n = i + 1;
                
                // 谐波和非谐波频率
                if (i < 8)
                {
                    // 前8个泛音使用谐波
                    _partialFreqRatios[i] = n;
                }
                else
                {
                    // 后8个泛音使用非谐波，创造更丰富的音色
                    double detune = 0.1 + _random.NextDouble() * 0.3;
                    _partialFreqRatios[i] = n * (1 + detune);
                }
                
                // 振幅包络：电子合成音色的泛音结构
                double amplitudeDecay = Math.Pow(n, -0.8);
                
                // 根据音高调整音色
                if (_noteRatio < 0.3) // 低音区
                {
                    amplitudeDecay *= (1.0 + 0.2 / n); // 增强低频泛音
                }
                else if (_noteRatio > 0.7) // 高音区
                {
                    amplitudeDecay *= (1.0 + 0.1 * (_noteRatio - 0.7)); // 增强高频
                }
                
                _partialAmplitudes[i] = amplitudeDecay;
                
                // 衰减率：高频泛音衰减更快
                _partialDecays[i] = 0.1 + n * 0.15 + n * n * 0.01;
                
                // 调制深度：为每个泛音添加不同的调制深度
                _partialModulation[i] = 0.01 + _random.NextDouble() * 0.03;
                
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

                if (timeMs >= _durationMs)
                {
                    _stopped = true;
                    break;
                }

                double sample = GenerateSynthSample(timeMs);
                double envelope = CalculateEnvelope(timeMs);

                float sampleValue = (float)(sample * envelope * _peakVolume);

                for (int c = 0; c < channels && i + c < sampleCount; c++)
                {
                    buffer[offset + i + c] = sampleValue;
                }

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

        private double GenerateSynthSample(double timeMs)
        {
            double sample = 0;
            double timeSec = timeMs / 1000.0;

            for (int i = 0; i < _partialPhases.Length; i++)
            {
                double partialFreq = _frequency * _partialFreqRatios[i];
                // 添加频率调制
                double modulation = _partialModulation[i] * Math.Sin(2.0 * Math.PI * (i + 1) * timeSec);
                double phase = _partialPhases[i] + (partialFreq + modulation) * timeSec * 2 * Math.PI;
                double amplitude = _partialAmplitudes[i] * Math.Exp(-_partialDecays[i] * timeSec);
                
                // 使用不同的波形
                if (i % 3 == 0)
                {
                    // 正弦波
                    sample += Math.Sin(phase) * amplitude;
                }
                else if (i % 3 == 1)
                {
                    // 方波
                    sample += (Math.Sin(phase) > 0 ? 0.8 : -0.8) * amplitude;
                }
                else
                {
                    double phaseNormalized = phase / (2 * Math.PI);
                    phaseNormalized = phaseNormalized - Math.Floor(phaseNormalized);
                    sample += (2.0 * phaseNormalized - 1.0) * amplitude;
                }
            }

            // 滤波效果
            double filter = 1.0 - Math.Exp(-timeSec / 0.5);
            sample *= filter;

            // 归一化
            sample = sample / _partialPhases.Length * 2.2;
            return Math.Clamp(sample, -1.0, 1.0);
        }

        private double CalculateEnvelope(double timeMs)
        {
            double releaseStart = _durationMs - ReleaseMs;

            if (releaseStart < AttackMs + DecayMs)
            {
                releaseStart = Math.Max(AttackMs + DecayMs, _durationMs * 0.8);
            }

            if (releaseStart >= _durationMs)
            {
                releaseStart = _durationMs * 0.9;
            }

            double releaseDuration = _durationMs - releaseStart;
            if (releaseDuration < 1)
            {
                releaseDuration = 1;
            }

            if (timeMs >= releaseStart)
            {
                double releaseProgress = (timeMs - releaseStart) / releaseDuration;
                releaseProgress = Math.Min(1.0, releaseProgress);
                return SustainLevel * Math.Exp(-4 * releaseProgress);
            }

            if (timeMs < AttackMs)
            {
                double attackProgress = timeMs / AttackMs;
                return Math.Pow(attackProgress, 0.6);
            }

            if (timeMs < AttackMs + DecayMs)
            {
                double decayProgress = (timeMs - AttackMs) / DecayMs;
                return 1.0 - (1.0 - SustainLevel) * decayProgress;
            }

            return SustainLevel;
        }
    }
}
