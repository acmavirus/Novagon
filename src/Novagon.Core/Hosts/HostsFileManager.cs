using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace Novagon.Core.Hosts;

/// <summary>
/// Quáº£n lÃ½ file C:\Windows\System32\drivers\etc\hosts má»™t cÃ¡ch an toÃ n.
/// Má»i tÃªn miá»n tá»± Ä‘á»™ng Ä‘á»u Ä‘Æ°á»£c giá»›i háº¡n trong khá»‘i ghi chÃº riÃªng biá»‡t Ä‘á»ƒ khÃ´ng áº£nh hÆ°á»Ÿng dá»¯ liá»‡u ngÆ°á»i dÃ¹ng.
/// </summary>
public static class HostsFileManager
{
    private static readonly string HostsPath = Path.Combine(
        System.Environment.GetFolderPath(System.Environment.SpecialFolder.System),
        "drivers", "etc", "hosts");

    private const string BlockStart = "# ===== BEGIN Novagon VHOSTS =====";
    private const string BlockEnd = "# ===== END Novagon VHOSTS =====";
    private static readonly object FileLock = new();

    /// <summary>
    /// Kiá»ƒm tra quyá»n ghi vÃ o file hosts.
    /// </summary>
    public static bool CanWriteHostsFile()
    {
        try
        {
            if (!File.Exists(HostsPath)) return false;
            using var fs = File.Open(HostsPath, FileMode.Open, FileAccess.ReadWrite);
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Äá»“ng bá»™ danh sÃ¡ch tÃªn miá»n vÃ o file hosts.
    /// </summary>
    public static bool SyncDomains(IEnumerable<string> domains, string ip = "127.0.0.1")
    {
        lock (FileLock)
        {
            try
            {
                if (!File.Exists(HostsPath))
                    return false;

                // Tá»± Ä‘á»™ng sao lÆ°u file hosts trÆ°á»›c khi sá»­a
                BackupHostsFile();

                string content = File.ReadAllText(HostsPath, Encoding.UTF8);

                int startIndex = content.IndexOf(BlockStart, StringComparison.OrdinalIgnoreCase);
                int endIndex = content.IndexOf(BlockEnd, StringComparison.OrdinalIgnoreCase);

                string beforeBlock;
                string afterBlock;

                if (startIndex != -1 && endIndex != -1 && endIndex > startIndex)
                {
                    beforeBlock = content[..startIndex].TrimEnd();
                    afterBlock = content[(endIndex + BlockEnd.Length)..].TrimStart();
                }
                else
                {
                    beforeBlock = content.TrimEnd();
                    afterBlock = string.Empty;
                }

                var cleanDomains = domains
                    .Where(d => !string.IsNullOrWhiteSpace(d))
                    .Select(d => d.Trim().ToLowerInvariant())
                    .Distinct()
                    .OrderBy(d => d)
                    .ToList();

                var newBlockBuilder = new StringBuilder();
                newBlockBuilder.AppendLine(BlockStart);
                foreach (var domain in cleanDomains)
                {
                    newBlockBuilder.AppendLine($"{ip,-15} {domain}");
                }
                newBlockBuilder.AppendLine(BlockEnd);

                var finalContent = new StringBuilder();
                if (!string.IsNullOrWhiteSpace(beforeBlock))
                {
                    finalContent.AppendLine(beforeBlock);
                    finalContent.AppendLine();
                }

                finalContent.Append(newBlockBuilder);

                if (!string.IsNullOrWhiteSpace(afterBlock))
                {
                    finalContent.AppendLine();
                    finalContent.Append(afterBlock);
                }

                File.WriteAllText(HostsPath, finalContent.ToString(), Encoding.UTF8);
                return true;
            }
            catch
            {
                return false;
            }
        }
    }

    private static void BackupHostsFile()
    {
        try
        {
            string backupDir = Path.Combine(AppDirectoryHelper.AppRoot, "backups");
            Directory.CreateDirectory(backupDir);
            string backupPath = Path.Combine(backupDir, $"hosts_{DateTime.Now:yyyyMMdd_HHmmss}.bak");
            File.Copy(HostsPath, backupPath, overwrite: true);
        }
        catch { }
    }
}

