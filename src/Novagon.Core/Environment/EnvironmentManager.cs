using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;

namespace Novagon.Core.Environment;

/// <summary>
/// Quáº£n lÃ½ viá»‡c khá»Ÿi Ä‘á»™ng Terminal (PowerShell, Windows Terminal, Cmd)
/// vá»›i biáº¿n mÃ´i trÆ°á»ng PATH táº¡m thá»i Ä‘Æ°á»£c bá»• sung cÃ¡c runtime PHP, MySQL, Composer, NodeJS.
/// </summary>
public static class EnvironmentManager
{
    public static void OpenTerminal(string? currentPhpDir = null, string? currentMySqlDir = null, string? workingDir = null)
    {
        string workDir = workingDir ?? Path.Combine(AppDirectoryHelper.AppRoot, "www");
        Directory.CreateDirectory(workDir);

        var extraPaths = new List<string>();

        if (!string.IsNullOrEmpty(currentPhpDir) && Directory.Exists(currentPhpDir))
        {
            extraPaths.Add(currentPhpDir);
        }

        if (!string.IsNullOrEmpty(currentMySqlDir) && Directory.Exists(currentMySqlDir))
        {
            string mysqlBin = Path.Combine(currentMySqlDir, "bin");
            extraPaths.Add(Directory.Exists(mysqlBin) ? mysqlBin : currentMySqlDir);
        }

        // ThÃªm thÆ° má»¥c bin/tools náº¿u cÃ³ (chá»©a composer.bat, v.v.)
        string toolsDir = Path.Combine(AppDirectoryHelper.AppRoot, "bin", "tools");
        if (Directory.Exists(toolsDir))
        {
            extraPaths.Add(toolsDir);
        }

        string systemPath = System.Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
        string combinedPath = string.Join(";", extraPaths) + ";" + systemPath;

        var psi = new ProcessStartInfo
        {
            FileName = "powershell.exe",
            WorkingDirectory = workDir,
            UseShellExecute = false
        };

        psi.EnvironmentVariables["PATH"] = combinedPath;

        System.Diagnostics.Process.Start(psi);
    }
}

