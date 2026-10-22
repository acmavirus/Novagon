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
    public string DisplayName { get; set; } = DisplayName;
    public bool IsInstalled { get; set; }
    public string? CustomActionText { get; set; }
    public string ActionText => CustomActionText ?? (IsInstalled ? "✓ Đã cài đặt" : "Tải về (1-Click)");
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
            new("php", "8.3.10", "PHP 8.3.10 (Khuyến nghị, NTS x64)",
                "https://downloads.php.net/~windows/releases/archives/php-8.3.10-nts-Win32-vs16-x64.zip",
                "php-8.3.10", "~32 MB"),

            new("php", "8.2.20", "PHP 8.2.20 (LTS ổn định, NTS x64)",
                "https://windows.php.net/downloads/releases/archives/php-8.2.20-nts-Win32-vs16-x64.zip",
                "php-8.2.20", "~31 MB"),

            new("php", "8.1.29", "PHP 8.1.29 (LTS, NTS x64)",
                "https://windows.php.net/downloads/releases/archives/php-8.1.29-nts-Win32-vs16-x64.zip",
                "php-8.1.29", "~30 MB"),

            new("mysql", "10.11.8", "MariaDB 10.11.8 (Tương thích MySQL 8.0, x64)",
                "https://archive.mariadb.org/mariadb-10.11.8/winx64-packages/mariadb-10.11.8-winx64.zip",
                "mariadb-10.11.8-winx64", "~85 MB"),

            new("redis", "5.0.14", "Redis 5.0.14 (In-memory Cache Server x64)",
                "https://github.com/tporadowski/redis/releases/download/v5.0.14.1/Redis-x64-5.0.14.1.zip",
                "redis-5.0.14", "~5 MB"),

            new("tools", "2.8.2", "Composer 2.8.2 (PHP Package Manager)",
                "https://getcomposer.org/download/latest-stable/composer.phar",
                "composer", "~3 MB"),

            new("nodejs", "20.18.0", "Node.js 20.18.0 LTS (với npm & npx)",
                "https://nodejs.org/dist/v20.18.0/node-v20.18.0-win-x64.zip",
                "node-v20", "~31 MB"),

            new("tools", "1.21.8", "Mailpit 1.21.8 (Local Email Catcher & Web UI)",
                "https://github.com/axllent/mailpit/releases/download/v1.21.8/mailpit-windows-amd64.zip",
                "mailpit", "~14 MB")
        };

        // Kiểm tra xem package nào đã cài đặt
        foreach (var pkg in packages)
        {
            if (pkg.Category == "tools" && pkg.TargetFolderName == "composer")
            {
                string pharPath = Path.Combine(_baseBinDir, "composer", "composer.phar");
                pkg.IsInstalled = File.Exists(pharPath);
            }
            else if (pkg.Category == "tools" && pkg.TargetFolderName == "mailpit")
            {
                string mailpitExe = Path.Combine(_baseBinDir, "tools", "mailpit", "mailpit.exe");
                pkg.IsInstalled = File.Exists(mailpitExe);
            }
            else
            {
                string pkgPath = Path.Combine(_baseBinDir, pkg.Category, pkg.TargetFolderName);
                pkg.IsInstalled = Directory.Exists(pkgPath);
            }
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
            onProgress?.Invoke(5, $"Đang kết nối tới máy chủ tải {pkg.DisplayName}...");

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
                            onProgress?.Invoke(pct, $"Đang tải {pkg.DisplayName}: {receivedBytes / 1024 / 1024} MB / {totalBytes / 1024 / 1024} MB ({pct}%)...");
                        }
                    }
                }
            }

            onProgress?.Invoke(92, "Đang giải nén gói runtime...");

            if (pkg.DownloadUrl.EndsWith(".phar", StringComparison.OrdinalIgnoreCase))
            {
                Directory.CreateDirectory(targetDir);
                string pharDest = Path.Combine(targetDir, "composer.phar");
                File.Copy(tempZip, pharDest, true);

                string batDest = Path.Combine(targetDir, "composer.bat");
                string batContent = "@echo off\r\nphp \"%~dp0composer.phar\" %*\r\n";
                File.WriteAllText(batDest, batContent);

                // Đồng thời đồng bộ sang bin/tools/
                string toolsDir = Path.Combine(_baseBinDir, "tools");
                Directory.CreateDirectory(toolsDir);
                File.Copy(batDest, Path.Combine(toolsDir, "composer.bat"), true);
                File.Copy(pharDest, Path.Combine(toolsDir, "composer.phar"), true);
            }
            else
            {
                // Giải nén gói vào thư mục đích
                if (Directory.Exists(targetDir))
                {
                    Directory.Delete(targetDir, true);
                }

                // Với MariaDB, file zip chứa sẵn thư mục con `mariadb-10.11.8-winx64/`
                if (pkg.Category == "mysql")
                {
                    ZipFile.ExtractToDirectory(tempZip, catDir, true);
                }
                else if (pkg.Category == "nodejs")
                {
                    ZipFile.ExtractToDirectory(tempZip, catDir, true);
                    string extractedNode = Path.Combine(catDir, "node-v20.18.0-win-x64");
                    if (Directory.Exists(extractedNode) && extractedNode != targetDir)
                    {
                        if (Directory.Exists(targetDir)) Directory.Delete(targetDir, true);
                        Directory.Move(extractedNode, targetDir);
                    }
                }
                else
                {
                    Directory.CreateDirectory(targetDir);
                    ZipFile.ExtractToDirectory(tempZip, targetDir, true);
                }
            }

            // Nếu là gói PHP, tự động sinh php.ini với cấu hình tối ưu
            if (pkg.Category == "php")
            {
                ConfigurePhpIni(targetDir);
            }

            onProgress?.Invoke(100, $"Đã cài đặt {pkg.DisplayName} thành công!");
            pkg.IsInstalled = true;
            return (true, $"Cài đặt {pkg.DisplayName} hoàn tất.");
        }
        catch (Exception ex)
        {
            return (false, $"Lỗi cài đặt {pkg.DisplayName}: {ex.Message}");
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

    public bool UninstallPackage(AvailablePackage pkg)
    {
        try
        {
            if (pkg.Category == "tools" && pkg.TargetFolderName == "composer")
            {
                string pharPath = Path.Combine(_baseBinDir, "composer", "composer.phar");
                string batPath = Path.Combine(_baseBinDir, "composer", "composer.bat");
                if (File.Exists(pharPath)) File.Delete(pharPath);
                if (File.Exists(batPath)) File.Delete(batPath);

                string toolsPhar = Path.Combine(_baseBinDir, "tools", "composer.phar");
                string toolsBat = Path.Combine(_baseBinDir, "tools", "composer.bat");
                if (File.Exists(toolsPhar)) File.Delete(toolsPhar);
                if (File.Exists(toolsBat)) File.Delete(toolsBat);

                string compDir = Path.Combine(_baseBinDir, "composer");
                if (Directory.Exists(compDir) && Directory.GetFileSystemEntries(compDir).Length == 0)
                {
                    try { Directory.Delete(compDir, true); } catch { }
                }
            }
            else if (pkg.Category == "tools" && pkg.TargetFolderName == "mailpit")
            {
                string mailpitDir = Path.Combine(_baseBinDir, "tools", "mailpit");
                if (Directory.Exists(mailpitDir))
                {
                    Directory.Delete(mailpitDir, true);
                }
            }
            else
            {
                string pkgPath = Path.Combine(_baseBinDir, pkg.Category, pkg.TargetFolderName);
                if (Directory.Exists(pkgPath))
                {
                    Directory.Delete(pkgPath, true);
                }
            }

            pkg.IsInstalled = false;
            return true;
        }
        catch
        {
            return false;
        }
    }
}
