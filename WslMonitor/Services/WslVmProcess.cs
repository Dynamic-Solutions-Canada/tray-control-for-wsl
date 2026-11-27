using System.Diagnostics;
using System.Runtime.InteropServices;

namespace WslMonitor.Services;

/// <summary>
/// Reads the WSL 2 virtual machine's processor time and memory from Windows' system-wide process
/// snapshot (<c>NtQuerySystemInformation</c>), the data Task Manager shows. The VM process
/// (<c>vmmemWSL</c>) refuses the process handle that <see cref="Process.TotalProcessorTime"/> needs
/// unless the app runs elevated, but the snapshot needs no handle. Nothing runs inside WSL, so reading
/// never keeps a distro awake.
/// <para>Holds a reusable buffer of a few hundred KB while in use; <see cref="Dispose"/> frees it.</para>
/// </summary>
public sealed class WslVmProcess : IDisposable
{
    private static readonly NLog.Logger Logger = NLog.LogManager.GetCurrentClassLogger();

    /// <summary>
    /// The WSL 2 VM's host process. Matched exactly: <c>vmmem</c> is other Hyper-V VMs (Docker's,
    /// Windows Sandbox's), and very old WSL releases that also ran as <c>vmmem</c> can't be told apart
    /// from them, so the usage strip stays hidden there.
    /// </summary>
    private const string ImageName = "vmmemWSL";

    private const int SystemProcessInformation = 5;
    private const int StatusInfoLengthMismatch = unchecked((int)0xC0000004);
    private const int InitialBufferBytes = 512 * 1024;
    private const int MaxBufferBytes = 64 * 1024 * 1024;

    // SYSTEM_PROCESS_INFORMATION field offsets on 64-bit Windows (the app ships x64 and ARM64 only).
    private const int NextEntryOffset = 0;
    private const int WorkingSetPrivateSizeOffset = 8;
    private const int UserTimeOffset = 40;
    private const int KernelTimeOffset = 48;
    private const int ImageNameLengthOffset = 56;
    private const int ImageNameBufferOffset = 64;
    private const int UniqueProcessIdOffset = 80;

    private IntPtr _buffer;
    private int _bufferBytes;
    private bool _failureLogged;

    /// <summary>The VM's current reading, or null when it isn't running or Windows wouldn't say.</summary>
    public WslVmSample? Read()
    {
        if (IntPtr.Size != 8)
        {
            return null;   // the offsets above are for 64-bit processes only
        }

        // Scaled in floating point: the raw timestamp times 10^7 would overflow a long after about a
        // day of uptime on Windows' 10 MHz counter.
        long wallTime = (long)(Stopwatch.GetTimestamp() * (10_000_000.0 / Stopwatch.Frequency));
        if (!TakeSnapshot())
        {
            return null;
        }

        return FindVm(wallTime);
    }

    /// <summary>Fill the buffer with the snapshot, growing it if Windows says it is too small.</summary>
    private bool TakeSnapshot()
    {
        if (_buffer == IntPtr.Zero)
        {
            Allocate(InitialBufferBytes);
        }

        while (true)
        {
            int status = NtQuerySystemInformation(SystemProcessInformation, _buffer, _bufferBytes, out int needed);
            if (status == 0)
            {
                return true;
            }

            // Processes can start between the size query and the read, so grow with headroom.
            bool canGrow = status == StatusInfoLengthMismatch && _bufferBytes < MaxBufferBytes;
            if (!canGrow)
            {
                LogFailureOnce($"NtQuerySystemInformation failed with 0x{status:X8}");
                return false;
            }

            Allocate(Math.Min(MaxBufferBytes, Math.Max(needed + 64 * 1024, _bufferBytes * 2)));
        }
    }

    /// <summary>Walk the snapshot's linked entries for the VM process.</summary>
    private WslVmSample? FindVm(long wallTime)
    {
        long offset = 0;
        while (offset < _bufferBytes)
        {
            IntPtr entry = _buffer + (nint)offset;
            if (IsVm(entry))
            {
                long cpu = Marshal.ReadInt64(entry, UserTimeOffset) + Marshal.ReadInt64(entry, KernelTimeOffset);
                long privateBytes = Marshal.ReadInt64(entry, WorkingSetPrivateSizeOffset);
                long pid = Marshal.ReadIntPtr(entry, UniqueProcessIdOffset).ToInt64();
                return new WslVmSample(pid, cpu, privateBytes, wallTime);
            }

            int next = Marshal.ReadInt32(entry, NextEntryOffset);
            if (next <= 0)
            {
                return null;
            }

            offset += next;
        }

        return null;
    }

    /// <summary>True when the entry's image name is exactly <see cref="ImageName"/> (case-insensitive).</summary>
    private static bool IsVm(IntPtr entry)
    {
        int lengthBytes = (ushort)Marshal.ReadInt16(entry, ImageNameLengthOffset);
        IntPtr name = Marshal.ReadIntPtr(entry, ImageNameBufferOffset);
        if (name == IntPtr.Zero || lengthBytes != ImageName.Length * 2)
        {
            return false;
        }

        return string.Equals(Marshal.PtrToStringUni(name, ImageName.Length), ImageName, StringComparison.OrdinalIgnoreCase);
    }

    private void Allocate(int bytes)
    {
        Free();
        _buffer = Marshal.AllocHGlobal(bytes);
        _bufferBytes = bytes;
    }

    private void Free()
    {
        if (_buffer != IntPtr.Zero)
        {
            Marshal.FreeHGlobal(_buffer);
            _buffer = IntPtr.Zero;
            _bufferBytes = 0;
        }
    }

    /// <summary>Log a failed read once per session, not once a second while the flyout is open.</summary>
    private void LogFailureOnce(string message)
    {
        if (_failureLogged)
        {
            return;
        }

        _failureLogged = true;
        Logger.Warn(message);
    }

    /// <summary>Free the snapshot buffer. The reader can be used again afterwards; it reallocates.</summary>
    public void Dispose() => Free();

    [DllImport("ntdll.dll")]
    private static extern int NtQuerySystemInformation(int systemInformationClass, IntPtr systemInformation, int length, out int returnLength);
}
