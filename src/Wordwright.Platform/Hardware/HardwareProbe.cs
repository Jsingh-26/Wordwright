using System.Management;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics.X86;
using Vortice.DXGI;
using Wordwright.Core.Hardware;

namespace Wordwright.Platform.Hardware;

/// <summary>
/// Reads what this PC has, for the tier rules in
/// <see cref="Wordwright.Core.Hardware.TierClassifier"/>
/// (docs/ARCHITECTURE.md → Hardware probing). Every part of it is best-effort:
/// a piece that cannot be read reports "none" rather than failing the whole
/// reading, because the answer only decides which model to offer.
/// </summary>
public static class HardwareProbe
{
    public static HardwareProfile Read() => new()
    {
        TotalRamBytes = Memory().Total,
        AvailableRamBytes = Memory().Available,
        CpuName = CpuName(),
        PhysicalCores = PhysicalCores(),
        HasAvx2 = Avx2.IsSupported,
        HasAvx512 = Avx512F.IsSupported,
        Gpus = Gpus(),
        FreeDiskBytes = FreeDiskBytes(),
        OsBuild = Environment.OSVersion.Version.ToString(3),
    };

    private static (ulong Total, ulong Available) Memory()
    {
        var status = new MemoryStatus { Length = (uint)Marshal.SizeOf<MemoryStatus>() };

        return GlobalMemoryStatusEx(ref status)
            ? (status.TotalPhysical, status.AvailablePhysical)
            : (0, 0);
    }

    private static string CpuName()
    {
        try
        {
            using var searcher = new ManagementObjectSearcher(
                "SELECT Name FROM Win32_Processor");

            foreach (var processor in searcher.Get())
            {
                using (processor)
                {
                    if (processor["Name"] is string name && name.Length > 0)
                    {
                        return name.Trim();
                    }
                }
            }
        }
        catch (ManagementException exception)
        {
            System.Diagnostics.Debug.WriteLine($"CPU name unavailable: {exception.Message}");
        }

        return "";
    }

    private static int PhysicalCores()
    {
        try
        {
            using var searcher = new ManagementObjectSearcher(
                "SELECT NumberOfCores FROM Win32_Processor");

            var cores = 0;
            foreach (var processor in searcher.Get())
            {
                using (processor)
                {
                    if (processor["NumberOfCores"] is { } value && int.TryParse(value.ToString(), out var count))
                    {
                        cores += count;
                    }
                }
            }

            if (cores > 0)
            {
                return cores;
            }
        }
        catch (ManagementException exception)
        {
            System.Diagnostics.Debug.WriteLine($"Core count unavailable: {exception.Message}");
        }

        // Logical processors are better than nothing.
        return Environment.ProcessorCount;
    }

    /// <summary>Adapters with memory of their own. The software adapter Windows
    /// falls back to when no driver is installed is skipped — it has no memory
    /// and cannot run a model (docs/ARCHITECTURE.md → Hardware probing).</summary>
    private static IReadOnlyList<GpuInfo> Gpus()
    {
        var gpus = new List<GpuInfo>();

        try
        {
            using var factory = DXGI.CreateDXGIFactory1<IDXGIFactory1>();

            for (var index = 0u; ; index++)
            {
                var result = factory.EnumAdapters1(index, out var adapter);
                if (result.Failure || adapter is null)
                {
                    break;
                }

                using (adapter)
                {
                    var description = adapter.Description1;
                    var memory = (ulong)description.DedicatedVideoMemory;

                    if ((description.Flags & AdapterFlags.Software) == 0 && memory > 0)
                    {
                        gpus.Add(new GpuInfo(description.Description.Trim(), memory));
                    }
                }
            }
        }
        catch (Exception exception) when (exception is SharpGen.Runtime.SharpGenException or DllNotFoundException)
        {
            // No DXGI (a locked-down or headless machine): nothing to report.
            System.Diagnostics.Debug.WriteLine($"GPU probe failed: {exception.Message}");
        }

        return gpus;
    }

    /// <summary>Free space on the drive the models are downloaded to
    /// (<c>%LocalAppData%</c>).</summary>
    private static long FreeDiskBytes()
    {
        try
        {
            var local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            var root = Path.GetPathRoot(local);

            return root is null ? 0 : new DriveInfo(root).AvailableFreeSpace;
        }
        catch (Exception exception) when (exception is ArgumentException or IOException)
        {
            return 0;
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MemoryStatus
    {
        public uint Length;
        public uint MemoryLoad;
        public ulong TotalPhysical;
        public ulong AvailablePhysical;
        public ulong TotalPageFile;
        public ulong AvailablePageFile;
        public ulong TotalVirtual;
        public ulong AvailableVirtual;
        public ulong AvailableExtendedVirtual;
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool GlobalMemoryStatusEx(ref MemoryStatus status);
}
