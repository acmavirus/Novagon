using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;

namespace Novagon.Core.Process;

public enum ProcessStatus
{
    Stopped,
    Starting,
    Running,
    Stopping,
    Faulted
}

/// <summary>
/// Quáº£n lÃ½ tiáº¿n trÃ¬nh dá»‹ch vá»¥ Ä‘á»™c láº­p, gáº¯n káº¿t vá»›i JobObjectManager vÃ  thu tháº­p log báº¥t Ä‘á»“ng bá»™.
/// </summary>
public class ProcessRunner : IDisposable
{
    private System.Diagnostics.Process? _process;
    private readonly object _lock = new();
    private bool _disposed;

    public string Name { get; }
    public string ExecutablePath { get; }
    public string Arguments { get; }
    public string WorkingDirectory { get; }
    public Dictionary<string, string> EnvironmentVariables { get; } = new(StringComparer.OrdinalIgnoreCase);

    public ProcessStatus Status { get; private set; } = ProcessStatus.Stopped;
    public int? ProcessId => _process?.Id;

    public event Action<string>? OutputDataReceived;
    public event Action<string>? ErrorDataReceived;
    public event Action<ProcessStatus>? StatusChanged;

    public ProcessRunner(string name, string executablePath, string arguments = "", string? workingDirectory = null)
    {
        Name = name;
        ExecutablePath = executablePath;
        Arguments = arguments;
        WorkingDirectory = workingDirectory ?? Path.GetDirectoryName(executablePath) ?? AppContext.BaseDirectory;
    }

    public bool Start()
    {
        lock (_lock)
        {
            if (Status == ProcessStatus.Running)
                return true;

            if (!File.Exists(ExecutablePath))
            {
                SetStatus(ProcessStatus.Faulted);
                ErrorDataReceived?.Invoke($"[Error] Tá»‡p thá»±c thi khÃ´ng tá»“n táº¡i: {ExecutablePath}");
                return false;
            }

            try
            {
                SetStatus(ProcessStatus.Starting);

                var startInfo = new ProcessStartInfo
                {
                    FileName = ExecutablePath,
                    Arguments = Arguments,
                    WorkingDirectory = WorkingDirectory,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    StandardOutputEncoding = Encoding.UTF8,
                    StandardErrorEncoding = Encoding.UTF8,
                    WindowStyle = ProcessWindowStyle.Hidden
                };

                foreach (var kv in EnvironmentVariables)
                {
                    startInfo.EnvironmentVariables[kv.Key] = kv.Value;
                }

                _process = new System.Diagnostics.Process
                {
                    StartInfo = startInfo,
                    EnableRaisingEvents = true
                };

                _process.OutputDataReceived += (_, e) =>
                {
                    if (e.Data != null) OutputDataReceived?.Invoke(e.Data);
                };

                _process.ErrorDataReceived += (_, e) =>
                {
                    if (e.Data != null) ErrorDataReceived?.Invoke(e.Data);
                };

                _process.Exited += (_, _) =>
                {
                    if (Status == ProcessStatus.Stopping) return;
                    int exitCode = -1;
                    try { exitCode = _process?.ExitCode ?? -1; } catch { }
                    SetStatus(exitCode != 0 ? ProcessStatus.Faulted : ProcessStatus.Stopped);
                };

                if (!_process.Start())
                {
                    SetStatus(ProcessStatus.Faulted);
                    return false;
                }

                // GÃ¡n ngay tiáº¿n trÃ¬nh vÃ o Job Object Ä‘á»ƒ trÃ¡nh rÃ² rá»‰ tiáº¿n trÃ¬nh
                JobObjectManager.Instance.AssignProcess(_process);

                _process.BeginOutputReadLine();
                _process.BeginErrorReadLine();

                SetStatus(ProcessStatus.Running);
                return true;
            }
            catch (Exception ex)
            {
                SetStatus(ProcessStatus.Faulted);
                ErrorDataReceived?.Invoke($"[Lá»—i khá»Ÿi Ä‘á»™ng {Name}]: {ex.Message}");
                return false;
            }
        }
    }

    public void Stop(int timeoutMilliseconds = 800)
    {
        System.Diagnostics.Process? procToKill = null;

        lock (_lock)
        {
            if (_process == null)
            {
                SetStatus(ProcessStatus.Stopped);
                return;
            }

            SetStatus(ProcessStatus.Stopping);
            procToKill = _process;
            _process = null;
        }

        if (procToKill != null)
        {
            try
            {
                procToKill.EnableRaisingEvents = false;
                if (!procToKill.HasExited)
                {
                    procToKill.Kill(entireProcessTree: true);
                }
            }
            catch { }
            finally
            {
                try { procToKill.Dispose(); } catch { }
            }
        }

        SetStatus(ProcessStatus.Stopped);
    }

    private void SetStatus(ProcessStatus newStatus)
    {
        Status = newStatus;
        try
        {
            StatusChanged?.Invoke(newStatus);
        }
        catch { }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        Stop();
        GC.SuppressFinalize(this);
    }
}

