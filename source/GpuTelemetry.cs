using System.Globalization;

namespace HudWall;

internal sealed record GpuSample(double? Util = null, double? TempC = null, double? VramPct = null);

internal sealed class GpuTelemetry : IDisposable
{
    private readonly Func<string> _query;
    private readonly Func<long> _clock;
    private readonly System.Threading.Timer _timer;
    private readonly object _sync = new();
    private GpuSample _sample = new();
    private long _sampleTime;
    private bool _paused, _disposed;
    private int _epoch;
    private int _polling;

    internal GpuTelemetry(Func<string> query = null, Func<long> clock = null, bool startTimer = true)
    {
        _query = query ?? (() => BoundedProcess.Run("nvidia-smi", "--id=0 --query-gpu=utilization.gpu,temperature.gpu,memory.used,memory.total --format=csv,noheader,nounits"));
        _clock = clock ?? (() => Environment.TickCount64);
        if (startTimer) _timer = new System.Threading.Timer(_ => Poll(), null, 300, 900);
    }

    internal void SetPaused(bool paused)
    {
        lock (_sync) { if (_paused != paused) _epoch++; _paused = paused; if (paused) _sample = new(); }
    }

    internal GpuSample Snapshot()
    {
        lock (_sync) return _disposed || _paused || _clock() - _sampleTime > 5000 ? new() : _sample;
    }

    internal void Poll()
    {
        if (Interlocked.CompareExchange(ref _polling, 1, 0) != 0) return;
        try
        {
            int epoch;
            lock (_sync) { if (_disposed || _paused) return; epoch = _epoch; }
            GpuSample next;
            try { next = Parse(_query()); }
            catch (Exception ex) { Program.Log("GPU telemetry: " + ex.Message); next = new(); }
            lock (_sync)
            {
                if (_disposed || _paused || epoch != _epoch) return;
                _sample = next;
                _sampleTime = _clock();
            }
        }
        finally { Volatile.Write(ref _polling, 0); }
    }

    internal static GpuSample Parse(string output)
    {
        var fields = output?.Split('\n', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault()?.Split(',');
        if (fields == null || fields.Length != 4) return new();
        double? Number(string value, double maximum) =>
            double.TryParse(value.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out double n) &&
            double.IsFinite(n) && n >= 0 && n <= maximum ? n : null;
        var used = Number(fields[2], double.MaxValue);
        var total = Number(fields[3], double.MaxValue);
        return new(Number(fields[0], 100), Number(fields[1], 200),
            used.HasValue && total > 0 && used <= total ? Math.Round(used.Value / total.Value * 100, 1) : null);
    }

    public void Dispose()
    {
        lock (_sync) { _disposed = true; _sample = new(); }
        _timer?.Dispose();
    }
}
