using System.Text.Json;
using HudWall;

using (var gpu = new NvidiaGpu())
{
    var sample = gpu.Sample();
    Console.WriteLine($"GPU: {gpu.Name ?? "unavailable"}; totalGiB={gpu.TotalGiB}; util={sample.Util}; tempC={sample.TempC}; vramPct={sample.VramPct}");
    if (sample.Util is < 0 or > 100 || sample.TempC is < 0 or > 150 || sample.VramPct is < 0 or > 100)
        throw new Exception("GPU values are outside their valid ranges.");
    if (Environment.GetEnvironmentVariable("HUDWALL_REQUIRE_NVIDIA") == "1" &&
        (string.IsNullOrWhiteSpace(gpu.Name) || gpu.TotalGiB is null || sample.TempC is null || sample.Util is null))
        throw new Exception("NVIDIA telemetry required but unavailable.");
    gpu.Dispose();
    if (gpu.Sample().TempC is not null) throw new Exception("Disposed reader returned live telemetry.");
}

using (var collector = new StatsCollector())
{
    Thread.Sleep(2500);
    using var snapshot = JsonDocument.Parse(collector.SnapshotJson());
    Console.WriteLine("Collector GPU: " + snapshot.RootElement.GetProperty("gpu"));
    collector.SetPaused(true);
    collector.Dispose();
    Thread.Sleep(2200); // A queued timer callback must remain safe during/after disposal.
}
Console.WriteLine("PASS GPU reader, collector JSON, optional telemetry and idempotent disposal");
