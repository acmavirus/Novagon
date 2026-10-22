using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Threading.Tasks;

namespace Novagon.Core.Runtimes;

public record AvailablePackage(
    string Category,
    string Version,
    string DisplayName,
    string DownloadUrl,
    string TargetFolderName,
    string SizeDescription
)
{
    public bool IsInstalled { get; set; }
    public string ActionText => IsInstalled ? "âœ“ ÄÃ£ cÃ i Ä‘áº·t" : "Táº£i vá» (1-Click)";
    public bool CanInstall => !IsInstalled;
}

public class PackageManager
{
    private static readonly HttpClient _httpClient = new() { Timeout = TimeSpan.FromMinutes(10) };
    private readonly string _baseBinDir;

    public PackageManager(string? baseBinDir = null)
    {
        _baseBinDir = baseBinDir ?? Path.Combine(AppDirectoryHelper.AppRoot, "bin");
    }

    public List<AvailablePackage> GetAvailablePackages()
    {
        var packages = new List<AvailablePackage>
        {
            new("php", "8.3.10", "PHP 8.3.10 (Khuyáº¿n nghá»‹, NTS x64)",
                "https://downloads.php.net/~windows/releases/archives/php-8.3.10-nts-Win32-vs16-x64.zip",
                "php-8.3.10", "~32 MB"),

            new("php", "8.2.20", "PHP 8.2.20 (LTS á»•n Ä‘á»‹nh, NTS x64)",
                "https://windows.php.net/downloads/releases/archives/php-8.2.20-nts-Win32-vs16-x64.zip",
                "php-8.2.20", "~31 MB"),

            new("php", "8.1.29", "PHP 8.1.29 (LTS, NTS x64)",
                "https://windows.php.net/downloads/releases/archives/php-8.1.29-nts-Win32-vs16-x64.zip",
                "php-8.1.29", "~30 MB"),

            new("mysql", "10.11.8", "MariaDB 10.11.8 (TÆ°Æ¡ng thÃ­ch MySQL 8.0, x64)",
                "https://archive.mariadb.org/mariadb-10.11.8/winx64-packages/mariadb-10.11.8-winx64.zip",
                "mariadb-10.11.8-winx64", "~85 MB"),

            new("redis", "5.0.14", "Redis 5.0.14 (In-memory Cache Server x64)",
                "https://github.com/tporadowski/redis/releases/download/v5.0.14.1/Redis-x64-5.0.14.1.zip",
                "redis-5.0.14", "~5 MB")
        };

        // Kiá»ƒm tra xem package nÃ o Ä‘Ã£ cÃ i Ä‘áº·t
        foreach (var pkg in packages)
        {
            string pkgPath = Path.Combine(_baseBinDir, pkg.Category, pkg.TargetFolderName);
            pkg.IsInstalled = Directory.Exists(pkgPath);
        }

        return packages;
    }

    public async Task<(bool Success, string Message)> DownloadAndInstallAsync(
        AvailablePackage pkg,
        Action<int, string>? onProgress = null)
    {
        string catDir = Path.Combine(_baseBinDir, pkg.Category);
        string targetDir = Path.Combine(catDir, pkg.TargetFolderName);
        string tempZip = Path.Combine(Path.GetTempPath(), $"novagon_{pkg.Category}_{pkg.Version}_{Guid.NewGuid():N}.zip");

        try
        {
            Directory.CreateDirectory(catDir);
            onProgress?.Invoke(5, $"Äang káº¿t ná»‘i tá»›i mÃ¡y chá»§ táº£i {pkg.DisplayName}...");

            using (var response = await _httpClient.GetAsync(pkg.DownloadUrl, HttpCompletionOption.ResponseHeadersRead))
            {
                response.EnsureSuccessStatusCode();

                long totalBytes = response.Content.Headers.ContentLength ?? -1;
                long receivedBytes = 0;

                await using var contentStream = await response.Content.ReadAsStreamAsync();
                await using var fileStream = new FileStream(tempZip, FileMode.Create, FileAccess.Write, FileShare.None, 8192, true);

                byte[] buffer = new byte[16384];
                int read;
                int lastPct = 0;

                while ((read = await contentStream.ReadAsync(buffer)) > 0)
                {
                    await fileStream.WriteAsync(buffer.AsMemory(0, read));
                    receivedBytes += read;

                    if (totalBytes > 0)
                    {
                        int pct = (int)((receivedBytes * 80) / totalBytes) + 10;
                        if (pct > lastPct && pct <= 90)
                        {
                            lastPct = pct;
                            onProgress?.Invoke(pct, $"Äang táº£i {pkg.DisplayName}: {receivedBytes / 1024 / 1024} MB / {totalBytes / 1024 / 1024} MB ({pct}%)...");
                        }
                    }
                }
            }

            onProgress?.Invoke(92, "Äang giáº£i nÃ©n gÃ³i runtime...");

            // Giáº£i nÃ©n gÃ³i vÃ o thÆ° má»¥c Ä‘Ã­ch
            if (Directory.Exists(targetDir))
            {
                Directory.Delete(targetDir, true);
            }

            // Vá»›i MariaDB, file zip chá»©a sáºµn thÆ° má»¥c con `mariadb-10.11.8-winx64/`
            if (pkg.Category == "mysql")
            {
                ZipFile.ExtractToDirectory(tempZip, catDir, true);
            }
            else
            {
                Directory.CreateDirectory(targetDir);
                ZipFile.ExtractToDirectory(tempZip, targetDir, true);
            }

            // Náº¿u lÃ  gÃ³i PHP, tá»± Ä‘á»™ng sinh php.ini vá»›i cáº¥u hÃ¬nh tá»‘i Æ°u
            if (pkg.Category == "php")
            {
                ConfigurePhpIni(targetDir);
            }

            onProgress?.Invoke(100, $"ÄÃ£ cÃ i Ä‘áº·t {pkg.DisplayName} thÃ nh cÃ´ng!");
            pkg.IsInstalled = true;
            return (true, $"CÃ i Ä‘áº·t {pkg.DisplayName} hoÃ n táº¥t.");
        }
        catch (Exception ex)
        {
            return (false, $"Lá»—i cÃ i Ä‘áº·t {pkg.DisplayName}: {ex.Message}");
        }
        finally
        {
            try
            {
                if (File.Exists(tempZip)) File.Delete(tempZip);
            }
            catch { }
        }
    }

    private static void ConfigurePhpIni(string phpDir)
    {
        try
        {
            string iniDev = Path.Combine(phpDir, "php.ini-development");
            string iniPath = Path.Combine(phpDir, "php.ini");

            if (File.Exists(iniDev) && !File.Exists(iniPath))
            {
                File.Copy(iniDev, iniPath, true);
            }

            if (File.Exists(iniPath))
            {
                string content = File.ReadAllText(iniPath);
                content = content.Replace(";extension_dir = \"ext\"", "extension_dir = \"ext\"");
                content = content.Replace(";extension=curl", "extension=curl");
                content = content.Replace(";extension=fileinfo", "extension=fileinfo");
                content = content.Replace(";extension=mbstring", "extension=mbstring");
                content = content.Replace(";extension=openssl", "extension=openssl");
                content = content.Replace(";extension=pdo_mysql", "extension=pdo_mysql");
                content = content.Replace(";extension=mysqli", "extension=mysqli");
                content = content.Replace(";extension=gd", "extension=gd");
                File.WriteAllText(iniPath, content);
            }
        }
        catch { }
    }
}

