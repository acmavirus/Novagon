using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Novagon.Core.Runtimes;

public record RuntimePackage(
    string Category,      // "php", "mysql", "nginx", "apache", "redis"
    string Name,          // "php-8.3.15", "mysql-8.4.0", "nginx-1.26.2"
    string RootPath,      // "C:\Novagon\bin\php\php-8.3.15"
    string ExecutablePath // "C:\Novagon\bin\php\php-8.3.15\php-cgi.exe"
);

/// <summary>
/// Quét các runtime portable trong thư mục bin/ tương tự như cơ chế của Novagon.
/// </summary>
public class RuntimeScanner
{
    public string BaseBinDirectory { get; }

    public RuntimeScanner(string? baseBinDirectory = null)
    {
        BaseBinDirectory = baseBinDirectory ?? Path.Combine(AppContext.BaseDirectory, "bin");
    }

    public List<RuntimePackage> GetInstalledPackages(string category)
    {
        var result = new List<RuntimePackage>();
        string categoryDir = Path.Combine(BaseBinDirectory, category.ToLowerInvariant());

        if (!Directory.Exists(categoryDir))
            return result;

        string exeName = category.ToLowerInvariant() switch
        {
            "php" => "php-cgi.exe",
            "mysql" => "mysqld.exe",
            "nginx" => "nginx.exe",
            "apache" => Path.Combine("bin", "httpd.exe"),
            "redis" => "redis-server.exe",
            _ => $"{category}.exe"
        };

        foreach (var subDir in Directory.GetDirectories(categoryDir))
        {
            string dirName = Path.GetFileName(subDir);
            string exePath = Path.Combine(subDir, exeName);

            // Náº¿u tÃ¬m theo subDir tháº¥t báº¡i, thá»­ kiá»ƒm tra trong subDir/bin/ (chuáº©n MariaDB/MySQL/Apache)
            if (!File.Exists(exePath))
            {
                string nestedBinPath = Path.Combine(subDir, "bin", Path.GetFileName(exeName));
                if (File.Exists(nestedBinPath))
                {
                    exePath = nestedBinPath;
                }
            }

            if (File.Exists(exePath))
            {
                result.Add(new RuntimePackage(category, dirName, subDir, exePath));
            }
        }

        return result.OrderByDescending(p => p.Name).ToList();
    }
}

