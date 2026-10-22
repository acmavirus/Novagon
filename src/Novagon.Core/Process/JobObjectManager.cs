using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace Novagon.Core.Process;

/// <summary>
/// Quáº£n lÃ½ Windows Job Object Ä‘á»ƒ Ä‘áº£m báº£o táº¥t cáº£ tiáº¿n trÃ¬nh con (Nginx, PHP-CGI, MySQL, Redis...)
/// Ä‘Æ°á»£c dá»n dáº¹p vÃ  káº¿t thÃºc 100% khi á»©ng dá»¥ng Novagon Ä‘Ã³ng hoáº·c gáº·p sá»± cá»‘ crash.
/// </summary>
public sealed class JobObjectManager : IDisposable
{
    private static readonly Lazy<JobObjectManager> _instance = new(() => new JobObjectManager());
    public static JobObjectManager Instance => _instance.Value;

    private IntPtr _jobHandle;
    private bool _disposed;

    public JobObjectManager()
    {
        _jobHandle = CreateJobObject(IntPtr.Zero, null);
        if (_jobHandle == IntPtr.Zero)
        {
            throw new Win32Exception(Marshal.GetLastWin32Error(), "KhÃ´ng thá»ƒ khá»Ÿi táº¡o Windows Job Object.");
        }

        var basicLimits = new JOBOBJECT_BASIC_LIMIT_INFORMATION
        {
            LimitFlags = JOB_OBJECT_LIMIT_KILL_ON_JOB_CLOSE
        };

        var extendedLimits = new JOBOBJECT_EXTENDED_LIMIT_INFORMATION
        {
            BasicLimitInformation = basicLimits
        };

        int length = Marshal.SizeOf(typeof(JOBOBJECT_EXTENDED_LIMIT_INFORMATION));
        IntPtr extendedLimitsPtr = Marshal.AllocHGlobal(length);
        try
        {
            Marshal.StructureToPtr(extendedLimits, extendedLimitsPtr, false);
            if (!SetInformationJobObject(_jobHandle, JobObjectInfoType.ExtendedLimitInformation, extendedLimitsPtr, (uint)length))
            {
                throw new Win32Exception(Marshal.GetLastWin32Error(), "KhÃ´ng thá»ƒ cáº¥u hÃ¬nh JOB_OBJECT_LIMIT_KILL_ON_JOB_CLOSE.");
            }
        }
        finally
        {
            Marshal.FreeHGlobal(extendedLimitsPtr);
        }
    }

    /// <summary>
    /// GÃ¡n má»™t tiáº¿n trÃ¬nh Ä‘ang cháº¡y vÃ o Job Object.
    /// </summary>
    public bool AssignProcess(System.Diagnostics.Process process)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(process);

        try
        {
            if (process.HasExited)
                return false;

            return AssignProcessToJobObject(_jobHandle, process.Handle);
        }
        catch
        {
            return false;
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        if (_jobHandle != IntPtr.Zero)
        {
            CloseHandle(_jobHandle);
            _jobHandle = IntPtr.Zero;
        }
        GC.SuppressFinalize(this);
    }

    ~JobObjectManager()
    {
        Dispose();
    }

    #region P/Invoke Definitions

    private const uint JOB_OBJECT_LIMIT_KILL_ON_JOB_CLOSE = 0x00002000;

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern IntPtr CreateJobObject(IntPtr lpJobAttributes, string? lpName);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool SetInformationJobObject(
        IntPtr hJob,
        JobObjectInfoType JobObjectInfoClass,
        IntPtr lpJobObjectInfo,
        uint cbJobObjectInfoLength);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool AssignProcessToJobObject(IntPtr hJob, IntPtr hProcess);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CloseHandle(IntPtr hObject);

    private enum JobObjectInfoType
    {
        ExtendedLimitInformation = 9
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct JOBOBJECT_BASIC_LIMIT_INFORMATION
    {
        public long PerProcessUserTimeLimit;
        public long PerJobUserTimeLimit;
        public uint LimitFlags;
        public UIntPtr MinimumWorkingSetSize;
        public UIntPtr MaximumWorkingSetSize;
        public uint ActiveProcessLimit;
        public UIntPtr Affinity;
        public uint PriorityClass;
        public uint SchedulingClass;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct IO_COUNTERS
    {
        public ulong ReadOperationCount;
        public ulong WriteOperationCount;
        public ulong OtherOperationCount;
        public ulong ReadTransferCount;
        public ulong WriteTransferCount;
        public ulong OtherTransferCount;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct JOBOBJECT_EXTENDED_LIMIT_INFORMATION
    {
        public JOBOBJECT_BASIC_LIMIT_INFORMATION BasicLimitInformation;
        public IO_COUNTERS IoInfo;
        public UIntPtr ProcessMemoryLimit;
        public UIntPtr JobMemoryLimit;
        public UIntPtr PeakProcessMemoryLimit;
        public UIntPtr PeakJobMemoryLimit;
    }

    #endregion
}

