using System;
using System.Runtime.InteropServices;
using System.Text;

namespace HudWall;

// Read the driver's first GPU directly. Never spawn a telemetry command or shell.
internal sealed class NvidiaGpu : IDisposable
{
    private readonly object _gate = new();
    private IntPtr _device;
    private bool _initialized;
    internal string Name { get; private set; }
    internal double? TotalGiB { get; private set; }

    internal NvidiaGpu()
    {
        try
        {
            if (nvmlInit_v2() != 0) return;
            _initialized = true;
            if (nvmlDeviceGetHandleByIndex_v2(0, out _device) != 0) _device = IntPtr.Zero;
            if (_device == IntPtr.Zero) { Dispose(); return; }
            var name = new StringBuilder(256);
            if (nvmlDeviceGetName(_device, name, (uint)name.Capacity) == 0) Name = name.ToString();
            if (nvmlDeviceGetMemoryInfo(_device, out var memory) == 0 && memory.Total > 0)
                TotalGiB = Math.Round(memory.Total / 1073741824.0, 1);
        }
        catch (Exception e) when (e is DllNotFoundException or EntryPointNotFoundException or BadImageFormatException)
        {
            Dispose(); // Missing/older NVIDIA runtime is optional, as GPU telemetry was before.
        }
    }

    internal StatsCollector.GpuSample Sample()
    {
        lock (_gate)
        {
            var sample = new StatsCollector.GpuSample();
            if (!_initialized || _device == IntPtr.Zero) return sample;
            if (nvmlDeviceGetUtilizationRates(_device, out var utilization) == 0)
                sample.Util = utilization.Gpu;
            if (nvmlDeviceGetTemperature(_device, 0, out var temperature) == 0)
                sample.TempC = temperature;
            if (nvmlDeviceGetMemoryInfo(_device, out var memory) == 0 && memory.Total > 0)
                sample.VramPct = Math.Round(memory.Used * 100.0 / memory.Total, 1);
            return sample; // Unsupported metrics stay unavailable, rather than pretending to be zero.
        }
    }

    public void Dispose()
    {
        lock (_gate)
        {
            if (!_initialized) return;
            _initialized = false;
            _device = IntPtr.Zero;
            try { nvmlShutdown(); } catch (EntryPointNotFoundException) { }
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct Memory { public ulong Total, Free, Used; }
    [StructLayout(LayoutKind.Sequential)]
    private struct Utilization { public uint Gpu, Memory; }

    // Load only the NVIDIA driver DLL installed in Windows System32, not PATH or the working folder.
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [DllImport("nvml.dll", CallingConvention = CallingConvention.Cdecl)]
    private static extern int nvmlInit_v2();
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [DllImport("nvml.dll", CallingConvention = CallingConvention.Cdecl)]
    private static extern int nvmlShutdown();
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [DllImport("nvml.dll", CallingConvention = CallingConvention.Cdecl)]
    private static extern int nvmlDeviceGetHandleByIndex_v2(uint index, out IntPtr device);
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [DllImport("nvml.dll", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    private static extern int nvmlDeviceGetName(IntPtr device, StringBuilder name, uint length);
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [DllImport("nvml.dll", CallingConvention = CallingConvention.Cdecl)]
    private static extern int nvmlDeviceGetMemoryInfo(IntPtr device, out Memory memory);
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [DllImport("nvml.dll", CallingConvention = CallingConvention.Cdecl)]
    private static extern int nvmlDeviceGetUtilizationRates(IntPtr device, out Utilization utilization);
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [DllImport("nvml.dll", CallingConvention = CallingConvention.Cdecl)]
    private static extern int nvmlDeviceGetTemperature(IntPtr device, uint sensor, out uint temperature);
}
