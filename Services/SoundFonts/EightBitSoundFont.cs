using System;
using NAudio.Wave;

namespace SkyscapeMidiDisplayer.Services.SoundFonts;

public class EightBitSoundFont : ISoundFont
{
    public string Name => "8Bit";

    public ISampleProvider CreateNoteProvider(double frequency, double volume, double durationMs, int noteNumber)
    {
        return new EightBitNoteProvider(frequency, volume, durationMs, noteNumber);
    }

    private class EightBitNoteProvider : ISampleProvider
    {
        private readonly double _frequency;
        private readonly double _peakVolume;
        private readonly double _durationMs;
        private readonly int _noteNumber;
        private double _phase;
        private double _samplePosition;
        private bool _stopped;

        private const double AttackMs = 5;
        private const double DecayMs = 100;
        private const double SustainLevel = 0.7;
        private const double ReleaseMs = 100;

        public event EventHandler? Finished;

        public WaveFormat WaveFormat { get; }

        public EightBitNoteProvider(double frequency, double volume, double durationMs, int noteNumber)
        {
            _frequency = frequency;
            _peakVolume = volume;
            _durationMs = durationMs;
            _noteNumber = noteNumber;
            WaveFormat = NAudio.Wave.WaveFormat.CreateIeeeFloatWaveFormat(48000, 2);
        }

        public void Stop()
        {
            _stopped = true;
            Finished?.Invoke(this, EventArgs.Empty);
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

                double sample = GenerateEightBitSample();
                double envelope = CalculateEnvelope(timeMs);

                float sampleValue = (float)(sample * envelope * _peakVolume);

                for (int c = 0; c < channels && i + c < sampleCount; c++)
                {
                    buffer[offset + i + c] = sampleValue;
                }

                _phase += _frequency / sampleRate;
                if (_phase >= 1.0)
                {
                    _phase -= 1.0;
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

        private double GenerateEightBitSample()
        {
            // 方波 - 8位音效的典型波形
            double squareWave = _phase < 0.5 ? 0.7 : -0.7;

            // 添加一点锯齿波成分，增加8位音效的质感
            double sawtoothWave = 2.0 * _phase - 1.0;

            // 混合波形
            double sample = squareWave * 0.8 + sawtoothWave * 0.2;

            // 量化效果 - 模拟8位音频的量化噪声
            int quantizationLevels = 16; // 8位音频通常有256个级别，但这里使用较少的级别来增强效果
            sample = Math.Round(sample * (quantizationLevels - 1)) / (quantizationLevels - 1);

            return sample;
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
                return SustainLevel * (1.0 - releaseProgress);
            }

            if (timeMs < AttackMs)
            {
                return timeMs / AttackMs;
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
