using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;

namespace Novagon.Core.Runtimes;

public record PhpExtensionItem(string Name, bool IsEnabled, string Description);

/// <summary>
/// Quáº£n lÃ½ báº­t/táº¯t cÃ¡c Extension PHP trong file php.ini má»™t cÃ¡ch trá»±c quan, 1-click.
/// </summary>
public class PhpExtensionManager
{
    private static readonly Dictionary<string, string> KnownExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        { "curl", "ThÆ° viá»‡n gá»­i HTTP Request / API cURL" },
        { "mbstring", "Xá»­ lÃ½ chuá»—i Unicode / UTF-8 Ä‘a byte" },
        { "mysqli", "Driver káº¿t ná»‘i MySQL cáº£i tiáº¿n" },
        { "pdo_mysql", "PDO Driver káº¿t ná»‘i MySQL/MariaDB (Laravel/WP)" },
        { "openssl", "MÃ£ hÃ³a SSL/TLS, HTTPS vÃ  báº£o máº­t" },
        { "fileinfo", "Nháº­n diá»‡n Ä‘á»‹nh dáº¡ng tá»‡p táº£i lÃªn (MIME type)" },
        { "gd", "Xá»­ lÃ½ cáº¯t ghÃ©p, nÃ©n vÃ  chá»‰nh sá»­a hÃ¬nh áº£nh" },
        { "zip", "Äá»c vÃ  giáº£i nÃ©n tá»‡p tin .zip" },
        { "intl", "Há»— trá»£ Ä‘a ngÃ´n ngá»¯ vÃ  Ä‘á»‹nh dáº¡ng tiá»n tá»‡/thá»i gian" },
        { "sqlite3", "CÆ¡ sá»Ÿ dá»¯ liá»‡u SQLite cá»¥c bá»™" },
        { "pdo_sqlite", "PDO Driver cho SQLite" },
        { "exif", "Äá»c siÃªu dá»¯ liá»‡u áº£nh mÃ¡y áº£nh (EXIF)" },
        { "soap", "Web Service giao thá»©c SOAP / XML" },
        { "sodium", "ThÆ° viá»‡n mÃ£ hÃ³a máº­t mÃ£ há»c hiá»‡n Ä‘áº¡i" }
    };

    public static List<PhpExtensionItem> GetExtensions(string phpRootPath)
    {
        var result = new List<PhpExtensionItem>();
        string iniPath = Path.Combine(phpRootPath, "php.ini");

        if (!File.Exists(iniPath))
        {
            // Náº¿u chÆ°a cÃ³ php.ini, thá»­ tÃ¬m php.ini-development
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
            // Khá»›p: extension=curl hoáº·c ;extension=curl hoáº·c extension=php_curl.dll
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
            result.Add(new PhpExtensionItem(kv.Key, isEnabled, kv.Value));
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

