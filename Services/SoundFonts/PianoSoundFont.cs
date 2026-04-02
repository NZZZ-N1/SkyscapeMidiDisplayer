using System;
using NAudio.Wave;

namespace SkyscapeMidiDisplayer.Services.SoundFonts;

public class PianoSoundFont : ISoundFont
{
    public string Name => "钢琴";

    public ISampleProvider CreateNoteProvider(double frequency, double volume, double durationMs, int noteNumber)
    {
        return new PianoNoteProvider(frequency, volume, durationMs, noteNumber);
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

        private double[] _partialPhases = Array.Empty<double>();
        private double[] _partialAmplitudes = Array.Empty<double>();
        private double[] _partialDecays = Array.Empty<double>();
        private double[] _partialFreqRatios = Array.Empty<double>();
        private double _noteRatio;

        private const double AttackMs = 3;
        private const double DecayMs = 200;
        private const double SustainLevel = 0.6;
        private const double ReleaseMs = 150;

        private static readonly Random _random = new();

        public event EventHandler? Finished;

        public WaveFormat WaveFormat { get; }

        public PianoNoteProvider(double frequency, double volume, double durationMs, int noteNumber)
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

                if (timeMs >= _durationMs)
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
            sample = sample / _partialPhases.Length * 3.0 + hammerNoise;
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
                return SustainLevel * Math.Exp(-5 * releaseProgress);
            }

            if (timeMs < AttackMs)
            {
                double attackProgress = timeMs / AttackMs;
                return Math.Pow(attackProgress, 0.3);
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
