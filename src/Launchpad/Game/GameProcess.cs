using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace Launchpad.Game;

public static partial class GameProcess
{
    private const string ProcessName = "Gorilla Tag";

    public static bool IsRunning()
    {
        if (OperatingSystem.IsWindows())
        {
            Process[] processes = Process.GetProcessesByName(ProcessName);

            foreach (Process process in processes)
                process.Dispose();

            return processes.Length > 0;
        }

        return LinuxPids().Any();
    }

    private static IEnumerable<int> LinuxPids()
    {
        if (!Directory.Exists("/proc"))
            yield break;

        foreach (var dir in Directory.EnumerateDirectories("/proc"))
        {
            if (!int.TryParse(Path.GetFileName(dir), out var pid))
                continue;

            string cmdline;

            try
            {
                cmdline = File.ReadAllText(Path.Combine(dir, "cmdline"));
            }
            catch (Exception)
            {
                continue;
            }

            if (cmdline.Contains(GameLocator.Executable, StringComparison.OrdinalIgnoreCase))
                yield return pid;
        }
    }

    public static bool? OwnsConnection(IPEndPoint remote, IPEndPoint local)
    {
        try
        {
            if (OperatingSystem.IsWindows())
            {
                var pid = WindowsOwner(remote, local);

                if (pid == null)
                    return null;

                using Process process = Process.GetProcessById(pid.Value);
                return process.ProcessName.Equals(ProcessName, StringComparison.OrdinalIgnoreCase);
            }

            if (OperatingSystem.IsLinux())
            {
                var pid = LinuxOwner(remote, local);
                return pid != null && LinuxPids().Contains(pid.Value) ? true : null;
            }
        }
        catch (Exception)
        {
        }

        return null;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct TcpRow
    {
        public uint State;
        public uint LocalAddr;
        public uint LocalPort;
        public uint RemoteAddr;
        public uint RemotePort;
        public uint OwningPid;
    }

    [LibraryImport("iphlpapi.dll")]
    private static partial uint GetExtendedTcpTable(IntPtr table, ref int size, [MarshalAs(UnmanagedType.Bool)] bool sort, int family, int tableClass, uint reserved);

    [SupportedOSPlatform("windows")]
    private static int? WindowsOwner(IPEndPoint remote, IPEndPoint local)
    {
        const int AfInet = 2, TcpTableOwnerPidConnections = 4;
        var size = 0;
        GetExtendedTcpTable(IntPtr.Zero, ref size, false, AfInet, TcpTableOwnerPidConnections, 0);
        var buffer = Marshal.AllocHGlobal(size);

        try
        {
            if (GetExtendedTcpTable(buffer, ref size, false, AfInet, TcpTableOwnerPidConnections, 0) != 0)
                return null;

            var count = Marshal.ReadInt32(buffer);
            var rowSize = Marshal.SizeOf<TcpRow>();

            for (var i = 0; i < count; i++)
            {
                TcpRow row = Marshal.PtrToStructure<TcpRow>(buffer + 4 + i * rowSize);
                int localPort = (ushort)IPAddress.NetworkToHostOrder((short)row.LocalPort);
                int remotePort = (ushort)IPAddress.NetworkToHostOrder((short)row.RemotePort);

                if (localPort == remote.Port && remotePort == local.Port)
                    return (int)row.OwningPid;
            }

            return null;
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }
    }

    private static int? LinuxOwner(IPEndPoint remote, IPEndPoint local)
    {
        string? inode = null;

        foreach (var line in File.ReadLines("/proc/net/tcp").Skip(1))
        {
            var fields = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);

            if (fields.Length < 10)
                continue;

            var localPort = Convert.ToInt32(fields[1].Split(':')[1], 16);
            var remotePort = Convert.ToInt32(fields[2].Split(':')[1], 16);

            if (localPort == remote.Port && remotePort == local.Port)
            {
                inode = fields[9];
                break;
            }
        }

        if (inode == null)
            return null;

        var target = $"socket:[{inode}]";

        foreach (var dir in Directory.EnumerateDirectories("/proc"))
        {
            if (!int.TryParse(Path.GetFileName(dir), out var pid))
                continue;

            try
            {
                foreach (var fd in Directory.EnumerateFiles(Path.Combine(dir, "fd")))
                {
                    if (new FileInfo(fd).LinkTarget == target)
                        return pid;
                }
            }
            catch (Exception)
            {
            }
        }

        return null;
    }
}
