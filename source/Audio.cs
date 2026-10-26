using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Threading;
using NAudio.CoreAudioApi;
using NAudio.Dsp;
using NAudio.Wave;

namespace HudWall
{
    internal interface ILoopbackSession : IDisposable
    {
        string DeviceName { get; }
        WaveFormat WaveFormat { get; }
        event EventHandler<WaveInEventArgs> DataAvailable;
        event EventHandler<StoppedEventArgs> RecordingStopped;
        void StartRecording();
    }

    internal sealed class LoopbackSession : ILoopbackSession
    {
        private readonly MMDevice _device;
        private readonly WasapiLoopbackCapture _capture;
        public string DeviceName => _device.FriendlyName;
        public WaveFormat WaveFormat => _capture.WaveFormat;
        public event EventHandler<WaveInEventArgs> DataAvailable;
        public event EventHandler<StoppedEventArgs> RecordingStopped;

        internal LoopbackSession(string deviceId)
        {
            using var enumerator = new MMDeviceEnumerator();
            _device = enumerator.GetDevice(deviceId);
            try { _capture = new WasapiLoopbackCapture(_device); }
            catch { _device.Dispose(); throw; }
            _capture.DataAvailable += (_, e) => DataAvailable?.Invoke(this, e);
            _capture.RecordingStopped += (_, e) => RecordingStopped?.Invoke(this, e);
        }

        public void StartRecording() => _capture.StartRecording();
        public void Dispose()
        {
            try { _capture.Dispose(); }
            finally { _device.Dispose(); }
        }

        internal static string DefaultDeviceId()
        {
            using var enumerator = new MMDeviceEnumerator();
            using var device = enumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);
            return device.ID;
        }
    }

    /// <summary>
    /// Captures the default playback device through WASAPI loopback and turns it
    /// into 56 log-spaced normalized bands, in-process (no microphone).
    /// </summary>
    internal sealed class AudioCapture : IDisposable
    {
        public const int Bands = 56;
        private const int Block = 2048;
        private const int FftPow = 11; // 2^11 = 2048

        private readonly object _sync = new();
        private readonly float[] _bands = new float[Bands];
        private readonly List<float> _pending = new(Block * 8);

        private readonly int[] _binStart = new int[Bands];
        private readonly int[] _binEnd = new int[Bands];

        private ILoopbackSession _cap;
        private readonly object _lifecycle = new();
        private readonly Func<string> _defaultDeviceId;
        private readonly Func<string, ILoopbackSession> _openSession;
        private string _deviceId;
        private volatile bool _disposed;
        private bool _ok;
        private string _err = "starting";
        private string _device = "default";
        private int _rate;
        private double _rms, _peak;
        private double _lastTs;
        private readonly System.Threading.Timer _retry;

        public AudioCapture() : this(LoopbackSession.DefaultDeviceId, id => new LoopbackSession(id)) { }

        internal AudioCapture(Func<string> defaultDeviceId, Func<string, ILoopbackSession> openSession, bool startTimer = true)
        {
            _defaultDeviceId = defaultDeviceId;
            _openSession = openSession;
            _retry = new System.Threading.Timer(_ => PollDevice(), null, Timeout.Infinite, Timeout.Infinite);
            if (startTimer) _retry.Change(0, 1500);
        }

        internal void PollDevice()
        {
            lock (_lifecycle)
            {
                if (_disposed) return;
                try
                {
                    string deviceId = _defaultDeviceId();
                    lock (_sync) { if (_cap != null && _ok && _deviceId == deviceId) return; }
                    ReleaseSession();
                    var cap = _openSession(deviceId);
                    lock (_sync)
                    {
                        _cap = cap;
                        _deviceId = deviceId;
                        _device = cap.DeviceName;
                        _rate = cap.WaveFormat.SampleRate;
                        BuildBandMap(_rate);
                    }
                    cap.DataAvailable += OnData;
                    cap.RecordingStopped += (s, e) =>
                    {
                        lock (_sync)
                        {
                            if (!ReferenceEquals(_cap, cap)) return;
                            _ok = false;
                            _err = e.Exception?.Message ?? "stopped";
                        }
                    };
                    lock (_sync) { _ok = true; _err = null; }
                    cap.StartRecording();
                }
                catch (Exception ex)
                {
                    ReleaseSession();
                    lock (_sync) { _ok = false; _err = ex.GetType().Name + ": " + ex.Message; }
                }
            }
        }

        // Call only while holding _lifecycle. Never dispose while holding _sync:
        // WASAPI disposal waits for callbacks that also take _sync.
        private void ReleaseSession()
        {
            ILoopbackSession cap;
            lock (_sync)
            {
                cap = _cap;
                _cap = null;
                _ok = false;
                _pending.Clear();
                Array.Clear(_bands);
                _rms = _peak = _lastTs = 0;
            }
            try { cap?.Dispose(); } catch { }
        }

        private void BuildBandMap(int rate)
        {
            double lo = 32.0;
            double hi = Math.Min(17000.0, rate / 2.0 - 100.0);
            double binHz = (double)rate / Block;
            for (int b = 0; b < Bands; b++)
            {
                double f0 = lo * Math.Pow(hi / lo, (double)b / Bands);
                double f1 = lo * Math.Pow(hi / lo, (double)(b + 1) / Bands);
                int s = (int)Math.Floor(f0 / binHz);
                int e = (int)Math.Ceiling(f1 / binHz);
                _binStart[b] = Math.Clamp(s, 1, Block / 2 - 1);
                _binEnd[b] = Math.Clamp(e, _binStart[b] + 1, Block / 2);
            }
        }

        private void OnData(object sender, WaveInEventArgs e)
        {
            if (_disposed) return;
            try
            {
                lock (_sync)
                {
                    if (_disposed || !ReferenceEquals(sender, _cap)) return;
                    var fmt = _cap.WaveFormat;
                    if (fmt.Encoding == WaveFormatEncoding.IeeeFloat && fmt.BitsPerSample == 32)
                    {
                        int n = e.BytesRecorded / 4;
                        int ch = Math.Max(1, fmt.Channels);
                        for (int i = 0; i + ch <= n; i += ch)
                        {
                            float sum = 0;
                            for (int c = 0; c < ch; c++) sum += BitConverter.ToSingle(e.Buffer, (i + c) * 4);
                            _pending.Add(sum / ch);
                        }
                    }
                    else if (fmt.BitsPerSample == 16)
                    {
                        int n = e.BytesRecorded / 2;
                        int ch = Math.Max(1, fmt.Channels);
                        for (int i = 0; i + ch <= n; i += ch)
                        {
                            float sum = 0;
                            for (int c = 0; c < ch; c++) sum += BitConverter.ToInt16(e.Buffer, (i + c) * 2) / 32768f;
                            _pending.Add(sum / ch);
                        }
                    }

                    while (_pending.Count >= Block)
                    {
                        ProcessBlock();
                        _pending.RemoveRange(0, Block);
                    }
                    if (_pending.Count > Block * 16) _pending.Clear();
                }
            }
            catch { /* keep the stream alive */ }
        }

        private void ProcessBlock()
        {
            var buf = new Complex[Block];
            double peak = 0, sumSq = 0;
            for (int i = 0; i < Block; i++)
            {
                float s = _pending[i];
                if (Math.Abs(s) > peak) peak = Math.Abs(s);
                sumSq += s * (double)s;
                buf[i].X = (float)(s * FastFourierTransform.HannWindow(i, Block));
                buf[i].Y = 0;
            }

            FastFourierTransform.FFT(true, FftPow, buf);

            // NAudio's forward FFT returns magnitudes normalized by 1/N, whereas the
            // numpy reference used an unnormalized rFFT scaled by N/4. Dividing by
            // 0.25 restores the same amplitude scale (a full-scale sine ~= 1.0).
            double normDiv = 0.25;
            for (int b = 0; b < Bands; b++)
            {
                double acc = 0;
                int count = 0;
                for (int k = _binStart[b]; k < _binEnd[b]; k++)
                {
                    double mag = Math.Sqrt(buf[k].X * (double)buf[k].X + buf[k].Y * (double)buf[k].Y) / normDiv;
                    acc += mag;
                    count++;
                }
                double band = count > 0 ? acc / count : 0;
                double db = 20.0 * Math.Log10(band + 1e-7);
                double norm = Math.Clamp((db + 72.0) / 62.0, 0.0, 1.0);
                _bands[b] = (float)(_bands[b] * 0.55 + norm * 0.45);
            }

            _rms = Math.Sqrt(sumSq / Block);
            _peak = peak;
            _lastTs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() / 1000.0;
        }

        public string SnapshotJson()
        {
            float[] bands;
            bool ok;
            string dev, err;
            int rate;
            double rms, peak, ts;
            lock (_sync)
            {
                bands = (float[])_bands.Clone();
                ok = _ok;
                dev = _device;
                err = _err;
                rate = _rate;
                rms = _rms;
                peak = _peak;
                ts = _lastTs;
            }
            double now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() / 1000.0;

            var sb = new StringBuilder(1024);
            sb.Append("{\"ok\":").Append(ok ? "true" : "false").Append(",\"bands\":[");
            for (int i = 0; i < bands.Length; i++)
            {
                if (i > 0) sb.Append(',');
                sb.Append(Math.Round(bands[i], 3).ToString(CultureInfo.InvariantCulture));
            }
            sb.Append("],\"rms\":").Append(Math.Round(rms, 5).ToString(CultureInfo.InvariantCulture));
            sb.Append(",\"peak\":").Append(Math.Round(peak, 4).ToString(CultureInfo.InvariantCulture));
            sb.Append(",\"device\":\"").Append(JsonEsc(dev)).Append('"');
            sb.Append(",\"rate\":").Append(rate);
            sb.Append(",\"err\":").Append(err == null ? "null" : "\"" + JsonEsc(err) + "\"");
            sb.Append(",\"ts\":").Append(ts.ToString(CultureInfo.InvariantCulture));
            sb.Append(",\"stale\":").Append((now - ts) > 2.0 ? "true" : "false");
            sb.Append('}');
            return sb.ToString();
        }

        public string DebugSummary()
        {
            lock (_sync)
            {
                float max = 0;
                foreach (var b in _bands) if (b > max) max = b;
                return string.Format(CultureInfo.InvariantCulture,
                    "ok={0} dev='{1}' rate={2} rms={3:F5} peak={4:F4} maxband={5:F3} err={6}",
                    _ok, _device, _rate, _rms, _peak, max, _err);
            }
        }

        private static string JsonEsc(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            return s.Replace("\\", "\\\\").Replace("\"", "\\\"");
        }

        public void Dispose()
        {
            lock (_lifecycle)
            {
                if (_disposed) return;
                _disposed = true;
                _retry.Dispose();
                ReleaseSession();
            }
        }
    }
}
