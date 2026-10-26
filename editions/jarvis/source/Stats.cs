using System.Drawing;
using System.Windows.Forms;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.NetworkInformation;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Threading;
using Microsoft.Win32;

namespace HudWall
{
    /// <summary>
    /// Collects the same JSON contract the HUD page already consumes, but
    /// entirely in-process (no Python, no HTTP service).
    /// </summary>
    internal sealed class StatsCollector : IDisposable
    {
        public const int CpuCoreCountMax = 256;

        private readonly object _sync = new object();
        private readonly Dictionary<string, object> _static = new();

        private long[] _prevIdle = Array.Empty<long>();
        private long[] _prevTotal = Array.Empty<long>();

        private long _prevNetRx, _prevNetTx, _prevNetTicks;

        private volatile GpuSample _gpu = new GpuSample();
        private System.Threading.Timer _gpuTimer;
        private readonly NvidiaGpu _nvidia = new();

        private volatile bool _paused;
        internal void SetPaused(bool paused) { _paused = paused; }
        private double _lastCpu;
        private long _lastProcs;

        internal sealed class GpuSample
        {
            public double? Util;
            public double? TempC;
            public double? VramPct;
        }

        public StatsCollector()
        {
            CollectStatic();
            SampleCpu(); // prime deltas
            _gpuTimer = new System.Threading.Timer(_ => PollGpu(), null, 300, 2000);
        }

        // ------------------------------------------------------------------ static
        private void CollectStatic()
        {
            string cpuName = ReadReg(@"HARDWARE\DESCRIPTION\System\CentralProcessor\0", "ProcessorNameString");
            if (string.IsNullOrWhiteSpace(cpuName)) cpuName = Environment.GetEnvironmentVariable("PROCESSOR_IDENTIFIER") ?? "CPU";
            cpuName = cpuName.Replace("(R)", "").Replace("(TM)", "").Replace("  ", " ").Trim();
            _static["cpu_name"] = cpuName;

            string osName = ReadReg(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion", "ProductName");
            string osVer = ReadReg(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion", "DisplayVersion");
            string osBuild = ReadReg(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion", "CurrentBuild");
            string installation = ReadReg(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion", "InstallationType");
            if (installation == "Client" && int.TryParse(osBuild, out int build) && build >= 22000)
                osName = (osName ?? "Windows 11").Replace("Windows 10", "Windows 11");
            if (!string.IsNullOrWhiteSpace(osName))
            {
                string os = osName.Replace("Microsoft ", "");
                if (!string.IsNullOrWhiteSpace(osVer)) os += " \u00b7 " + osVer;
                _static["os"] = os;
            }
            else if (!string.IsNullOrWhiteSpace(osBuild)) _static["os"] = "Windows " + osBuild;

            _static["cores"] = PhysicalCoreGuess();
            _static["threads"] = Environment.ProcessorCount;

            long totalRam = 0;
            var mem = new MEMORYSTATUSEX { dwLength = (uint)Marshal.SizeOf<MEMORYSTATUSEX>() };
            if (GlobalMemoryStatusEx(ref mem)) totalRam = (long)mem.ullTotalPhys;
            _static["ram_total_gb"] = Math.Round(totalRam / 1073741824.0, 1);

            string gpuName = _nvidia.Name;
            string gpuVram = _nvidia.TotalGiB?.ToString(CultureInfo.InvariantCulture);

            if (string.IsNullOrWhiteSpace(gpuName))
                gpuName = ReadReg(@"SYSTEM\CurrentControlSet\Control\Class\{4d36e968-e325-11ce-bfc1-08002be10318}\0000", "DriverDesc");

            _static["gpu_name"] = gpuName ?? "--";
            if (gpuVram != null) _static["gpu_vram_gb"] = gpuVram;
        }

        private static int PhysicalCoreGuess()
        {
            try
            {
                return int.Parse(ReadReg(@"HARDWARE\DESCRIPTION\System\CentralProcessor\0", "~MHz") ?? "") > 0
                    ? Environment.ProcessorCount / 2
                    : Environment.ProcessorCount / 2;
            }
            catch { return Environment.ProcessorCount / 2; }
        }

        private static string ReadReg(string path, string name)
        {
            try
            {
                using var k = Registry.LocalMachine.OpenSubKey(path);
                return k?.GetValue(name) as string;
            }
            catch { return null; }
        }

        // --------------------------------------------------------------------- cpu
        [StructLayout(LayoutKind.Sequential)]
        private struct ProcessorPerf
        {
            public long IdleTime;
            public long KernelTime;
            public long UserTime;
            public long DpcTime;
            public long InterruptTime;
            public uint InterruptCount;
        }

        [DllImport("ntdll.dll")]
        private static extern int NtQuerySystemInformation(int infoClass, IntPtr buffer, int length, out int returned);

        private const int SystemProcessorPerformanceInformation = 8;

        private (double total, double[] cores) SampleCpu()
        {
            int size = Marshal.SizeOf<ProcessorPerf>();
            IntPtr buf = Marshal.AllocHGlobal(size * CpuCoreCountMax);
            try
            {
                int status = NtQuerySystemInformation(SystemProcessorPerformanceInformation, buf, size * CpuCoreCountMax, out int returned);
                if (status != 0) return (_lastCpu, Array.Empty<double>());

                int count = returned / size;
                var idle = new long[count];
                var total = new long[count];
                for (int i = 0; i < count; i++)
                {
                    var p = Marshal.PtrToStructure<ProcessorPerf>(buf + i * size);
                    idle[i] = p.IdleTime;
                    total[i] = p.KernelTime + p.UserTime;
                }

                var cores = new double[count];
                double sumIdle = 0, sumTotal = 0;
                for (int i = 0; i < count; i++)
                {
                    if (_prevTotal.Length == count)
                    {
                        double dt = total[i] - _prevTotal[i];
                        double di = idle[i] - _prevIdle[i];
                        cores[i] = dt > 0 ? Math.Round(Math.Clamp((1.0 - di / dt) * 100.0, 0, 100), 1) : 0;
                    }
                    else cores[i] = 0;
                    sumIdle += idle[i];
                    sumTotal += total[i];
                }

                double usage = 0;
                if (_prevTotal.Length == count)
                {
                    double dt = sumTotal - _prevTotal.Sum();
                    double di = sumIdle - _prevIdle.Sum();
                    if (dt > 0) usage = Math.Round(Math.Clamp((1.0 - di / dt) * 100.0, 0, 100), 1);
                }

                _prevIdle = idle;
                _prevTotal = total;
                _lastCpu = usage;
                return (usage, cores);
            }
            catch { return (_lastCpu, Array.Empty<double>()); }
            finally { Marshal.FreeHGlobal(buf); }
        }

        // --------------------------------------------------------------------- gpu
        private void PollGpu()
        {
            if (_paused) return;
            try
            {
                _gpu = _nvidia.Sample();
            }
            catch { /* Optional driver telemetry: keep the last available sample. */ }
        }

        // ------------------------------------------------------------------- json
        public string SnapshotJson()
        {
            double cpu;
            double[] cores;
            lock (_sync) { (cpu, cores) = SampleCpu(); }

            var mem = new MEMORYSTATUSEX { dwLength = (uint)Marshal.SizeOf<MEMORYSTATUSEX>() };
            GlobalMemoryStatusEx(ref mem);

            var disks = new List<object>();
            try
            {
                foreach (var d in DriveInfo.GetDrives())
                {
                    if (d.DriveType != DriveType.Fixed || !d.IsReady) continue;
                    long total = d.TotalSize;
                    long free = d.TotalFreeSpace;
                    if (total <= 0) continue;
                    disks.Add(new
                    {
                        mount = d.Name.TrimEnd('\\'),
                        percent = Math.Round((1.0 - (double)free / total) * 100.0, 1),
                        used_gb = Math.Round((total - free) / 1073741824.0, 1),
                        total_gb = Math.Round(total / 1073741824.0, 1),
                    });
                }
            }
            catch { }

            double down = 0, up = 0;
            try
            {
                long rx = 0, tx = 0;
                foreach (var ni in NetworkInterface.GetAllNetworkInterfaces())
                {
                    if (ni.OperationalStatus != OperationalStatus.Up) continue;
                    if (ni.NetworkInterfaceType == NetworkInterfaceType.Loopback) continue;
                    var st = ni.GetIPv4Statistics();
                    rx += st.BytesReceived;
                    tx += st.BytesSent;
                }
                long now = Environment.TickCount64;
                if (_prevNetTicks > 0)
                {
                    double dt = Math.Max((now - _prevNetTicks) / 1000.0, 0.001);
                    down = Math.Round(Math.Max(0, rx - _prevNetRx) * 8 / dt / 1e6, 2);
                    up = Math.Round(Math.Max(0, tx - _prevNetTx) * 8 / dt / 1e6, 2);
                }
                _prevNetRx = rx; _prevNetTx = tx; _prevNetTicks = now;
            }
            catch { }

            long uptime = Environment.TickCount64 / 1000;
            try { var processes=Process.GetProcesses(); _lastProcs=processes.Length; foreach(var process in processes)process.Dispose(); } catch { }

            var g = _gpu;
            var payload = new Dictionary<string, object>(_static)
            {
                ["cpu"] = cpu,
                ["cpu_cores"] = cores,
                ["mem"] = new
                {
                    percent = mem.dwMemoryLoad,
                    used_gb = Math.Round((mem.ullTotalPhys - mem.ullAvailPhys) / 1073741824.0, 2),
                    total_gb = Math.Round(mem.ullTotalPhys / 1073741824.0, 1),
                },
                ["disks"] = disks,
                ["net"] = new { down_mbps = down, up_mbps = up },
                ["power"] = ReadPower(),
                ["gpu"] = new { util = g.Util, temp_c = g.TempC, vram_pct = g.VramPct },
                ["uptime_s"] = uptime,
                ["procs"] = _lastProcs,
                ["ts"] = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() / 1000.0,
            };

            return JsonSerializer.Serialize(payload);
        }

        private static object ReadPower() {
            var power=System.Windows.Forms.SystemInformation.PowerStatus;
            bool available=power.BatteryChargeStatus!=System.Windows.Forms.BatteryChargeStatus.Unknown && (power.BatteryChargeStatus & System.Windows.Forms.BatteryChargeStatus.NoSystemBattery)==0;
            return new { ac=power.PowerLineStatus.ToString(), battery_percent=available && power.BatteryLifePercent>=0 && power.BatteryLifePercent<=1 ? (int?)Math.Round(power.BatteryLifePercent*100):null };
        }

        public void Dispose()
        {
            _gpuTimer?.Dispose();
            _nvidia.Dispose();
        }

        // -------------------------------------------------------------- interop
        [StructLayout(LayoutKind.Sequential)]
        private struct MEMORYSTATUSEX
        {
            public uint dwLength;
            public uint dwMemoryLoad;
            public ulong ullTotalPhys;
            public ulong ullAvailPhys;
            public ulong ullTotalPageFile;
            public ulong ullAvailPageFile;
            public ulong ullTotalVirtual;
            public ulong ullAvailVirtual;
            public ulong ullAvailExtendedVirtual;
        }

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool GlobalMemoryStatusEx(ref MEMORYSTATUSEX lpBuffer);
    }
}
