using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;

namespace Novagon.Core.Runtimes;

public record PhpExtensionItem(string Name, bool IsEnabled, string Description);

/// <summary>
/// Quản lý bật/tắt các Extension PHP trong file php.ini một cách trực quan, 1-click.
/// </summary>
public class PhpExtensionManager
{
    private static readonly Dictionary<string, (string Vi, string En)> KnownExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        { "curl", ("Thư viện gửi HTTP Request / API cURL", "HTTP Request / cURL API library") },
        { "mbstring", ("Xử lý chuỗi Unicode / UTF-8 đa byte", "Multibyte string / UTF-8 Unicode processing") },
        { "mysqli", ("Driver kết nối MySQL cải tiến", "Improved MySQL database driver") },
        { "pdo_mysql", ("PDO Driver kết nối MySQL/MariaDB (Laravel/WP)", "PDO Driver for MySQL/MariaDB (Laravel/WP)") },
        { "openssl", ("Mã hóa SSL/TLS, HTTPS và bảo mật", "SSL/TLS encryption, HTTPS and cryptography") },
        { "fileinfo", ("Nhận diện định dạng tệp tải lên (MIME type)", "File MIME type inspection and detection") },
        { "gd", ("Xử lý cắt ghép, nén và chỉnh sửa hình ảnh", "Image processing, cropping, compression (GD)") },
        { "zip", ("Đọc và giải nén tệp tin .zip", "Read and extract .zip archive files") },
        { "intl", ("Hỗ trợ đa ngôn ngữ và định dạng tiền tệ/thời gian", "Internationalization / localization currency & date formatting") },
        { "sqlite3", ("Cơ sở dữ liệu SQLite cục bộ", "Embedded local SQLite3 database") },
        { "pdo_sqlite", ("PDO Driver cho SQLite", "PDO Driver for SQLite databases") },
        { "exif", ("Đọc siêu dữ liệu ảnh máy ảnh (EXIF)", "Read camera EXIF metadata from images") },
        { "soap", ("Web Service giao thức SOAP / XML", "SOAP / XML Web Service protocol") },
        { "sodium", ("Thư viện mã hóa mật mã học hiện đại", "Modern high-security cryptographic library") }
    };

    public static List<PhpExtensionItem> GetExtensions(string phpRootPath, bool isEn = false)
    {
        var result = new List<PhpExtensionItem>();
        string iniPath = Path.Combine(phpRootPath, "php.ini");

        if (!File.Exists(iniPath))
        {
            // Nếu chưa có php.ini, thử tìm php.ini-development
            string devIni = Path.Combine(phpRootPath, "php.ini-development");
            if (File.Exists(devIni))
            {
                File.Copy(devIni, iniPath, true);
            }
            else
            {
                return result;
            }
        }

        string[] lines = File.ReadAllLines(iniPath);
        var activeSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var seenSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (string line in lines)
        {
            string trimmed = line.Trim();
            // Khớp: extension=curl hoặc ;extension=curl hoặc extension=php_curl.dll
            var match = Regex.Match(trimmed, @"^(;?)\s*extension\s*=\s*(?:php_)?([a-zA-Z0-9_]+)(?:\.dll)?$", RegexOptions.IgnoreCase);
            if (match.Success)
            {
                bool isCommented = match.Groups[1].Value == ";";
                string extName = match.Groups[2].Value.ToLowerInvariant();
                seenSet.Add(extName);
                if (!isCommented)
                {
                    activeSet.Add(extName);
                }
            }
        }

        foreach (var kv in KnownExtensions)
        {
            bool isEnabled = activeSet.Contains(kv.Key);
            string desc = isEn ? kv.Value.En : kv.Value.Vi;
            result.Add(new PhpExtensionItem(kv.Key, isEnabled, desc));
        }

        return result;
    }

    public static bool ToggleExtension(string phpRootPath, string extName, bool enable)
    {
        string iniPath = Path.Combine(phpRootPath, "php.ini");
        if (!File.Exists(iniPath)) return false;

        string[] lines = File.ReadAllLines(iniPath);
        bool found = false;
        var newLines = new List<string>();

        string pattern = $@"^(;?)\s*extension\s*=\s*(?:php_)?{Regex.Escape(extName)}(?:\.dll)?$";

        foreach (string line in lines)
        {
            string trimmed = line.Trim();
            if (Regex.IsMatch(trimmed, pattern, RegexOptions.IgnoreCase))
            {
                found = true;
                if (enable)
                {
                    newLines.Add($"extension={extName}");
                }
                else
                {
                    newLines.Add($";extension={extName}");
                }
            }
            else
            {
                newLines.Add(line);
            }
        }

        if (!found && enable)
        {
            newLines.Add($"extension={extName}");
        }

        File.WriteAllLines(iniPath, newLines);
        return true;
    }
}
