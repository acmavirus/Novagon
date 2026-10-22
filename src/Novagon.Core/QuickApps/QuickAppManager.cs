using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Novagon.Core.Network;

namespace Novagon.Core.QuickApps;

public class QuickAppManager
{
    private readonly string _wwwRoot;
    private readonly string _cacheDir;
    private static readonly HttpClient HttpClient = new() { Timeout = TimeSpan.FromMinutes(5) };

    public event Action<string>? OnLog;

    public QuickAppManager(string? wwwRoot = null)
    {
        _wwwRoot = wwwRoot ?? Path.Combine(AppDirectoryHelper.AppRoot, "www");
        _cacheDir = Path.Combine(AppDirectoryHelper.AppRoot, "etc", "cache");
        Directory.CreateDirectory(_cacheDir);
    }

    /// <summary>
    /// Chuáº©n hÃ³a tÃªn database tá»« tÃªn dá»± Ã¡n (vÃ­ dá»¥ "my-blog" -> "my_blog")
    /// </summary>
    public static string NormalizeDbName(string projectName)
    {
        return projectName.Replace('-', '_').Replace(' ', '_').ToLowerInvariant();
    }

    /// <summary>
    /// Tá»± Ä‘á»™ng táº¡o MySQL/MariaDB database náº¿u server Ä‘ang cháº¡y
    /// </summary>
    public bool EnsureDatabaseCreated(string dbName)
    {
        try
        {
            if (!PortChecker.IsPortInUse(3306))
            {
                OnLog?.Invoke($"[DB] Lá»—i: MySQL chÆ°a cháº¡y trÃªn cá»•ng 3306. KhÃ´ng thá»ƒ táº¡o Database '{dbName}'.");
                return false;
            }

            string mysqlBinDir = Path.Combine(AppDirectoryHelper.AppRoot, "bin", "mysql");
            string? mysqlExe = null;
            if (Directory.Exists(mysqlBinDir))
            {
                var files = Directory.GetFiles(mysqlBinDir, "mysql.exe", SearchOption.AllDirectories);
                if (files.Length > 0) mysqlExe = files[0];
            }

            if (mysqlExe == null || !File.Exists(mysqlExe))
            {
                OnLog?.Invoke($"[DB] Lá»—i: KhÃ´ng tÃ¬m tháº¥y tá»‡p thá»±c thi mysql.exe trong {mysqlBinDir}");
                return false;
            }

            var psi = new ProcessStartInfo
            {
                FileName = mysqlExe,
                Arguments = $"-h 127.0.0.1 -P 3306 -u root -e \"CREATE DATABASE IF NOT EXISTS `{dbName}` CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci;\"",
                CreateNoWindow = true,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };

            using var proc = System.Diagnostics.Process.Start(psi);
            if (proc == null)
            {
                OnLog?.Invoke($"[DB] Lá»—i: KhÃ´ng thá»ƒ khá»Ÿi cháº¡y tiáº¿n trÃ¬nh mysql.exe");
                return false;
            }

            proc.WaitForExit(5000);
            if (proc.ExitCode == 0)
            {
                OnLog?.Invoke($"[DB] âœ“ ÄÃ£ tá»± Ä‘á»™ng táº¡o Database MySQL: '{dbName}'");
                return true;
            }

            string err = proc.StandardError.ReadToEnd().Trim();
            OnLog?.Invoke($"[DB] Lá»—i khi táº¡o Database '{dbName}' (ExitCode: {proc.ExitCode}): {err}");
            return false;
        }
        catch (Exception ex)
        {
            OnLog?.Invoke($"[DB] Ngoáº¡i lá»‡ khi táº¡o Database: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Táº¡o dá»± Ã¡n PHP Blank thuáº§n vá»›i giao diá»‡n kiá»ƒm tra server hiá»‡n Ä‘áº¡i
    /// </summary>
    public bool CreateBlankPhpApp(string projectName, out string projectDir)
    {
        return CreateBlankPhpApp(projectName, createDb: false, out projectDir);
    }

    public bool CreateBlankPhpApp(string projectName, bool createDb, out string projectDir)
    {
        projectDir = Path.Combine(_wwwRoot, projectName);
        if (Directory.Exists(projectDir))
        {
            OnLog?.Invoke($"Lá»—i: ThÆ° má»¥c dá»± Ã¡n '{projectName}' Ä‘Ã£ tá»“n táº¡i trong www/");
            return false;
        }

        Directory.CreateDirectory(projectDir);
        string dbName = NormalizeDbName(projectName);

        if (createDb)
        {
            if (!EnsureDatabaseCreated(dbName))
            {
                try { Directory.Delete(projectDir, true); } catch { }
                return false;
            }
        }

        string indexPhpContent = $@"<!DOCTYPE html>
<html lang=""vi"">
<head>
    <meta charset=""UTF-8"">
    <meta name=""viewport"" content=""width=device-width, initial-scale=1.0"">
    <title>{projectName} - Novagon Local Development</title>
    <style>
        * {{ margin: 0; padding: 0; box-sizing: border-box; }}
        body {{ font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, Helvetica, Arial, sans-serif; background: #f8fafc; color: #0f172a; padding: 40px 20px; }}
        .card {{ max-width: 720px; margin: 0 auto; background: #ffffff; border: 1px solid #cbd5e1; border-radius: 8px; padding: 32px; box-shadow: 0 4px 6px -1px rgba(0,0,0,0.05); }}
        h1 {{ font-size: 24px; color: #0f172a; margin-bottom: 8px; display: flex; align-items: center; gap: 10px; }}
        .badge {{ background: #16a34a; color: #ffffff; font-size: 12px; font-weight: bold; padding: 3px 8px; border-radius: 4px; }}
        p {{ color: #475569; font-size: 14px; margin-bottom: 24px; }}
        .info-grid {{ display: grid; grid-template-columns: 1fr 1fr; gap: 12px; margin-bottom: 24px; }}
        .info-box {{ background: #f1f5f9; padding: 14px; border-radius: 6px; border: 1px solid #e2e8f0; }}
        .info-title {{ font-size: 11px; font-weight: bold; color: #0369a1; text-transform: uppercase; margin-bottom: 4px; }}
        .info-val {{ font-size: 14px; font-weight: bold; color: #0f172a; word-break: break-all; }}
        .status-ok {{ color: #16a34a; font-weight: bold; }}
        .status-err {{ color: #dc2626; font-weight: bold; }}
        .btn-group {{ display: flex; gap: 10px; }}
        .btn {{ display: inline-block; background: #0284c7; color: #ffffff; padding: 10px 18px; border-radius: 6px; text-decoration: none; font-weight: bold; font-size: 14px; }}
        .btn:hover {{ background: #0369a1; }}
        .btn-secondary {{ background: #e2e8f0; color: #0f172a; }}
        .btn-secondary:hover {{ background: #cbd5e1; }}
    </style>
</head>
<body>
    <div class=""card"">
        <h1>{projectName} <span class=""badge"">Äang hoáº¡t Ä‘á»™ng</span></h1>
        <p>Dá»± Ã¡n Ä‘Æ°á»£c khá»Ÿi táº¡o thÃ nh cÃ´ng bá»Ÿi <strong>Novagon Quick App</strong>.</p>

        <div class=""info-grid"">
            <div class=""info-box"">
                <div class=""info-title"">PhiÃªn báº£n PHP</div>
                <div class=""info-val""><?= phpversion(); ?></div>
            </div>
            <div class=""info-box"">
                <div class=""info-title"">Web Server</div>
                <div class=""info-val""><?= $_SERVER['SERVER_SOFTWARE'] ?? 'Nginx'; ?></div>
            </div>
            <div class=""info-box"">
                <div class=""info-title"">Document Root</div>
                <div class=""info-val""><?= __DIR__; ?></div>
            </div>
            <div class=""info-box"">
                <div class=""info-title"">Database MySQL (127.0.0.1:3306)</div>
                <div class=""info-val"">
                    <?php
                    try {{
                        $pdo = new PDO('mysql:host=127.0.0.1;port=3306', 'root', '', [PDO::ATTR_TIMEOUT => 2]);
                        echo '<span class=""status-ok"">âœ“ Káº¿t ná»‘i thÃ nh cÃ´ng</span>';
                    }} catch (Exception $e) {{
                        echo '<span class=""status-err"">ChÆ°a káº¿t ná»‘i: ' . htmlspecialchars($e->getMessage()) . '</span>';
                    }}
                    ?>
                </div>
            </div>
        </div>

        <div class=""btn-group"">
            <a href=""?phpinfo=1"" class=""btn"" target=""_blank"">Xem cáº¥u hÃ¬nh PHP Info &rarr;</a>
            <a href=""http://127.0.0.1/phpmyadmin"" class=""btn btn-secondary"" target=""_blank"">Má»Ÿ phpMyAdmin &rarr;</a>
        </div>
        <?php if (isset($_GET['phpinfo'])) {{ phpinfo(); exit; }} ?>
    </div>
</body>
</html>";

        File.WriteAllText(Path.Combine(projectDir, "index.php"), indexPhpContent);
        OnLog?.Invoke($"[QuickApp] ÄÃ£ táº¡o thÃ nh cÃ´ng dá»± Ã¡n Blank PHP '{projectName}' táº¡i {projectDir}");
        return true;
    }

    /// <summary>
    /// TÃ¬m tá»‡p thá»±c thi php.exe trong bin/php
    /// </summary>
    public static string? FindPhpExecutable()
    {
        string phpBinDir = Path.Combine(AppDirectoryHelper.AppRoot, "bin", "php");
        if (Directory.Exists(phpBinDir))
        {
            var files = Directory.GetFiles(phpBinDir, "php.exe", SearchOption.AllDirectories);
            if (files.Length > 0) return files[0];
        }
        return null;
    }

    /// <summary>
    /// TÃ¬m hoáº·c kiá»ƒm tra composer.phar portable
    /// </summary>
    public static string? FindComposerPhar()
    {
        // 1. Kiá»ƒm tra bin/composer/composer.phar trong AppRoot
        string localPhar = Path.Combine(AppDirectoryHelper.AppRoot, "bin", "composer", "composer.phar");
        if (File.Exists(localPhar)) return localPhar;

        // 2. Kiá»ƒm tra D:\laragon\bin\composer\composer.phar náº¿u cÃ³
        string altLaragon = @"D:\laragon\bin\composer\composer.phar";
        if (File.Exists(altLaragon)) return altLaragon;

        return null;
    }

    /// <summary>
    /// Táº¡o dá»± Ã¡n Laravel chuáº©n 100% báº±ng Composer (composer create-project laravel/laravel)
    /// </summary>
    public async Task<(bool Success, string Message, string ProjectDir)> CreateLaravelAppAsync(
        string projectName,
        bool createDb,
        Action<int, string>? progressCallback = null)
    {
        string projectDir = Path.Combine(_wwwRoot, projectName);
        try
        {
            if (Directory.Exists(projectDir))
            {
                return (false, $"ThÆ° má»¥c dá»± Ã¡n '{projectName}' Ä‘Ã£ tá»“n táº¡i trong www/", projectDir);
            }

            string dbName = NormalizeDbName(projectName);

            // 1. Tá»± Ä‘á»™ng táº¡o Database MySQL náº¿u Ä‘Æ°á»£c chá»n
            if (createDb)
            {
                progressCallback?.Invoke(10, $"Äang táº¡o Database MySQL '{dbName}'...");
                if (!EnsureDatabaseCreated(dbName))
                {
                    string err = $"KhÃ´ng thá»ƒ táº¡o Database '{dbName}'. QuÃ¡ trÃ¬nh cÃ i Ä‘áº·t Ä‘Ã£ dá»«ng láº¡i.";
                    OnLog?.Invoke($"[QuickApp] âœ— {err}");
                    return (false, err, projectDir);
                }
            }

            // 2. TÃ¬m PHP vÃ  Composer
            progressCallback?.Invoke(15, "Äang chuáº©n bá»‹ mÃ´i trÆ°á»ng PHP vÃ  Composer...");
            string? phpExe = FindPhpExecutable();
            if (phpExe == null)
            {
                return (false, "KhÃ´ng tÃ¬m tháº¥y php.exe trong bin/php/", projectDir);
            }

            string? composerPhar = FindComposerPhar();
            if (composerPhar == null)
            {
                progressCallback?.Invoke(20, "Äang táº£i bá»™ náº¡p Composer má»›i nháº¥t tá»« getcomposer.org...");
                OnLog?.Invoke("[QuickApp] Äang táº£i composer.phar tá»« https://getcomposer.org/download/latest-stable/composer.phar...");
                string composerDir = Path.Combine(AppDirectoryHelper.AppRoot, "bin", "composer");
                Directory.CreateDirectory(composerDir);
                composerPhar = Path.Combine(composerDir, "composer.phar");
                using var response = await HttpClient.GetAsync("https://getcomposer.org/download/latest-stable/composer.phar");
                response.EnsureSuccessStatusCode();
                await using var fs = new FileStream(composerPhar, FileMode.Create, FileAccess.Write, FileShare.None);
                await response.Content.CopyToAsync(fs);
            }

            // 3. Thá»±c thi Composer create-project
            progressCallback?.Invoke(25, "Äang khá»Ÿi cháº¡y 'composer create-project laravel/laravel'...");
            OnLog?.Invoke($"[Composer] Báº¯t Ä‘áº§u táº¡o dá»± Ã¡n Laravel: composer create-project laravel/laravel {projectName} --prefer-dist");

            var psi = new ProcessStartInfo
            {
                FileName = phpExe,
                Arguments = $"\"{composerPhar}\" create-project laravel/laravel \"{projectName}\" --prefer-dist --no-interaction",
                WorkingDirectory = _wwwRoot,
                CreateNoWindow = true,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };

            // ÄÆ°a thÆ° má»¥c chá»©a php.exe vÃ o biáº¿n mÃ´i trÆ°á»ng PATH Ä‘á»ƒ script háº­u cÃ i Ä‘áº·t cá»§a Composer cháº¡y Ä‘Æ°á»£c php
            string? phpDir = Path.GetDirectoryName(phpExe);
            if (phpDir != null)
            {
                string existingPath = System.Environment.GetEnvironmentVariable("PATH") ?? "";
                psi.EnvironmentVariables["PATH"] = phpDir + ";" + existingPath;
            }

            using var proc = new System.Diagnostics.Process { StartInfo = psi };
            var outputBuilder = new StringBuilder();
            var errorBuilder = new StringBuilder();

            proc.OutputDataReceived += (s, e) =>
            {
                if (!string.IsNullOrWhiteSpace(e.Data))
                {
                    outputBuilder.AppendLine(e.Data);
                    OnLog?.Invoke($"[Composer] {e.Data}");
                    // Cáº­p nháº­t tiáº¿n trÃ¬nh Æ°á»›c tÃ­nh dá»±a trÃªn cÃ¡c bÆ°á»›c cá»§a Composer
                    if (e.Data.Contains("Installing dependencies", StringComparison.OrdinalIgnoreCase))
                        progressCallback?.Invoke(40, "Composer: Äang táº£i cÃ¡c gÃ³i dependencies cá»§a Laravel...");
                    else if (e.Data.Contains("Generating optimized", StringComparison.OrdinalIgnoreCase))
                        progressCallback?.Invoke(70, "Composer: Äang sinh autoload tá»‘i Æ°u...");
                    else if (e.Data.Contains("artisan", StringComparison.OrdinalIgnoreCase))
                        progressCallback?.Invoke(85, "Composer: Äang khá»Ÿi táº¡o á»©ng dá»¥ng qua Artisan...");
                    else
                        progressCallback?.Invoke(50, $"Composer: {e.Data}");
                }
            };

            proc.ErrorDataReceived += (s, e) =>
            {
                if (!string.IsNullOrWhiteSpace(e.Data))
                {
                    errorBuilder.AppendLine(e.Data);
                    OnLog?.Invoke($"[Composer] {e.Data}");
                }
            };

            proc.Start();
            proc.BeginOutputReadLine();
            proc.BeginErrorReadLine();

            await proc.WaitForExitAsync();

            if (proc.ExitCode != 0)
            {
                string err = errorBuilder.ToString().Trim();
                if (string.IsNullOrWhiteSpace(err)) err = outputBuilder.ToString().Trim();
                OnLog?.Invoke($"[Composer] âœ— Tháº¥t báº¡i (ExitCode: {proc.ExitCode}): {err}");
                return (false, $"Lá»—i cÃ i Ä‘áº·t Laravel báº±ng Composer (ExitCode {proc.ExitCode}): {err}", projectDir);
            }

            // 4. Äá»“ng bá»™ cáº¥u hÃ¬nh Database vÃ o .env cá»§a Laravel
            progressCallback?.Invoke(92, "Äang Ä‘á»“ng bá»™ cáº¥u hÃ¬nh Database vÃ  Domain vÃ o .env...");
            string envFile = Path.Combine(projectDir, ".env");
            if (File.Exists(envFile))
            {
                string envContent = File.ReadAllText(envFile);
                envContent = UpdateEnvKey(envContent, "DB_CONNECTION", "mysql");
                envContent = UpdateEnvKey(envContent, "DB_HOST", "127.0.0.1");
                envContent = UpdateEnvKey(envContent, "DB_PORT", "3306");
                envContent = UpdateEnvKey(envContent, "DB_DATABASE", dbName);
                envContent = UpdateEnvKey(envContent, "DB_USERNAME", "root");
                envContent = UpdateEnvKey(envContent, "DB_PASSWORD", "");
                envContent = UpdateEnvKey(envContent, "APP_URL", $"http://{projectName}.test");
                File.WriteAllText(envFile, envContent);
                OnLog?.Invoke($"[QuickApp] âœ“ ÄÃ£ cáº­p nháº­t file .env vá»›i DB_DATABASE='{dbName}'");
            }

            progressCallback?.Invoke(100, "HoÃ n táº¥t táº¡o dá»± Ã¡n Laravel chuáº©n 100%!");
            OnLog?.Invoke($"[QuickApp] âœ“ Táº¡o thÃ nh cÃ´ng dá»± Ã¡n Laravel '{projectName}' Ä‘áº§y Ä‘á»§ framework vÃ  packages.");
            return (true, "Khá»Ÿi táº¡o dá»± Ã¡n Laravel báº±ng Composer thÃ nh cÃ´ng!", projectDir);
        }
        catch (Exception ex)
        {
            OnLog?.Invoke($"[QuickApp] Lá»—i táº¡o Laravel: {ex.Message}");
            return (false, $"Lá»—i cÃ i Ä‘áº·t Laravel: {ex.Message}", projectDir);
        }
    }

    private static string UpdateEnvKey(string content, string key, string value)
    {
        var regex = new System.Text.RegularExpressions.Regex($@"^{key}=.*$", System.Text.RegularExpressions.RegexOptions.Multiline);
        if (regex.IsMatch(content))
        {
            return regex.Replace(content, $"{key}={value}");
        }
        return content + $"\n{key}={value}";
    }

    /// <summary>
    /// Táº¡o khung dá»± Ã¡n Laravel Starter Ä‘áº§y Ä‘á»§ chuáº©n hÃ³a
    /// </summary>
    public bool CreateLaravelApp(string projectName, bool createDb, out string projectDir)
    {
        projectDir = Path.Combine(_wwwRoot, projectName);
        if (Directory.Exists(projectDir))
        {
            OnLog?.Invoke($"Lá»—i: ThÆ° má»¥c dá»± Ã¡n '{projectName}' Ä‘Ã£ tá»“n táº¡i trong www/");
            return false;
        }

        Directory.CreateDirectory(projectDir);
        string dbName = NormalizeDbName(projectName);

        if (createDb)
        {
            if (!EnsureDatabaseCreated(dbName))
            {
                try { Directory.Delete(projectDir, true); } catch { }
                return false;
            }
        }

        // Táº¡o cáº¥u trÃºc thÆ° má»¥c Laravel
        string appDir = Path.Combine(projectDir, "app");
        string bootstrapDir = Path.Combine(projectDir, "bootstrap");
        string configDir = Path.Combine(projectDir, "config");
        string publicDir = Path.Combine(projectDir, "public");
        string resourcesDir = Path.Combine(projectDir, "resources", "views");
        string routesDir = Path.Combine(projectDir, "routes");
        string storageDir = Path.Combine(projectDir, "storage", "logs");

        Directory.CreateDirectory(appDir);
        Directory.CreateDirectory(bootstrapDir);
        Directory.CreateDirectory(configDir);
        Directory.CreateDirectory(publicDir);
        Directory.CreateDirectory(resourcesDir);
        Directory.CreateDirectory(routesDir);
        Directory.CreateDirectory(storageDir);

        // Sinh APP_KEY ngáº«u nhiÃªn
        byte[] keyBytes = new byte[32];
        RandomNumberGenerator.Fill(keyBytes);
        string appKey = "base64:" + Convert.ToBase64String(keyBytes);

        // File .env
        string envContent = $@"APP_NAME={projectName}
APP_ENV=local
APP_KEY={appKey}
APP_DEBUG=true
APP_TIMEZONE=UTC
APP_URL=http://{projectName}.test

LOG_CHANNEL=stack
LOG_DEPRECATIONS_CHANNEL=null
LOG_LEVEL=debug

DB_CONNECTION=mysql
DB_HOST=127.0.0.1
DB_PORT=3306
DB_DATABASE={dbName}
DB_USERNAME=root
DB_PASSWORD=

SESSION_DRIVER=file
SESSION_LIFETIME=120
";
        File.WriteAllText(Path.Combine(projectDir, ".env"), envContent);
        File.WriteAllText(Path.Combine(projectDir, ".env.example"), envContent);

        // File public/index.php giao diá»‡n chÃ o má»«ng phong cÃ¡ch Laravel hiá»‡n Ä‘áº¡i
        string publicIndex = $@"<!DOCTYPE html>
<html lang=""vi"">
<head>
    <meta charset=""UTF-8"">
    <meta name=""viewport"" content=""width=device-width, initial-scale=1.0"">
    <title>{projectName} - Laravel Local Environment</title>
    <style>
        * {{ margin: 0; padding: 0; box-sizing: border-box; }}
        body {{ font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, Helvetica, Arial, sans-serif; background: #0f172a; color: #f8fafc; padding: 40px 20px; }}
        .container {{ max-width: 800px; margin: 0 auto; }}
        .header {{ display: flex; align-items: center; justify-content: space-between; margin-bottom: 28px; padding-bottom: 18px; border-bottom: 1px solid #334155; }}
        .logo {{ font-size: 26px; font-weight: 800; color: #ef4444; display: flex; align-items: center; gap: 8px; }}
        .badge {{ background: #ef4444; color: #ffffff; font-size: 12px; font-weight: bold; padding: 4px 10px; border-radius: 9999px; }}
        .card {{ background: #1e293b; border: 1px solid #334155; border-radius: 12px; padding: 28px; margin-bottom: 20px; }}
        .title {{ font-size: 18px; font-weight: bold; color: #f8fafc; margin-bottom: 14px; }}
        .grid {{ display: grid; grid-template-columns: repeat(auto-fit, minmax(220px, 1fr)); gap: 14px; margin-bottom: 20px; }}
        .item {{ background: #0f172a; padding: 14px; border-radius: 8px; border: 1px solid #334155; }}
        .item-label {{ font-size: 11px; text-transform: uppercase; color: #94a3b8; font-weight: bold; margin-bottom: 4px; }}
        .item-val {{ font-size: 14px; font-weight: bold; color: #38bdf8; word-break: break-all; }}
        .ok {{ color: #22c55e; }}
        .err {{ color: #f87171; }}
        .btn {{ display: inline-block; background: #ef4444; color: #ffffff; padding: 10px 18px; border-radius: 6px; text-decoration: none; font-weight: bold; font-size: 14px; }}
        .btn:hover {{ background: #dc2626; }}
    </style>
</head>
<body>
    <div class=""container"">
        <div class=""header"">
            <div class=""logo"">LARAVEL <span class=""badge"">v11.x Starter</span></div>
            <div>Dá»± Ã¡n: <strong>{projectName}</strong></div>
        </div>

        <div class=""card"">
            <div class=""title"">ThÃ´ng sá»‘ mÃ´i trÆ°á»ng cá»¥c bá»™ (Novagon)</div>
            <div class=""grid"">
                <div class=""item"">
                    <div class=""item-label"">PHP Runtime</div>
                    <div class=""item-val""><?= phpversion(); ?></div>
                </div>
                <div class=""item"">
                    <div class=""item-label"">Document Root</div>
                    <div class=""item-val""><?= __DIR__; ?></div>
                </div>
                <div class=""item"">
                    <div class=""item-label"">Database ('{dbName}')</div>
                    <div class=""item-val"">
                        <?php
                        try {{
                            $pdo = new PDO('mysql:host=127.0.0.1;port=3306;dbname={dbName}', 'root', '', [PDO::ATTR_TIMEOUT => 2]);
                            echo '<span class=""ok"">âœ“ Sáºµn sÃ ng káº¿t ná»‘i</span>';
                        }} catch (Exception $e) {{
                            echo '<span class=""err"">ChÆ°a sáºµn sÃ ng: ' . htmlspecialchars($e->getMessage()) . '</span>';
                        }}
                        ?>
                    </div>
                </div>
            </div>

            <p style=""font-size: 13px; color: #94a3b8; margin-bottom: 18px;"">
                ThÆ° má»¥c <code>public/</code> Ä‘Ã£ Ä‘Æ°á»£c Novagon tá»± Ä‘á»™ng nháº­n diá»‡n vÃ  cáº¥u hÃ¬nh Virtual Host <code>{projectName}.test</code>.
            </p>
            <a href=""http://127.0.0.1/phpmyadmin"" class=""btn"" target=""_blank"">Quáº£n lÃ½ Database trong phpMyAdmin &rarr;</a>
        </div>
    </div>
</body>
</html>";
        File.WriteAllText(Path.Combine(publicDir, "index.php"), publicIndex);

        // File routes/web.php
        string routesWeb = @"<?php
// Routes cho á»©ng dá»¥ng
// Báº¡n cÃ³ thá»ƒ cÃ i Ä‘áº·t Composer vÃ  náº¡p trá»n bá»™ vendor báº¥t cá»© lÃºc nÃ o.
";
        File.WriteAllText(Path.Combine(routesDir, "web.php"), routesWeb);

        OnLog?.Invoke($"[QuickApp] ÄÃ£ táº¡o thÃ nh cÃ´ng dá»± Ã¡n Laravel '{projectName}' táº¡i {projectDir}");
        return true;
    }

    /// <summary>
    /// Táº¡o dá»± Ã¡n Frontend tÄ©nh HTML5/CSS3/JS hiá»‡n Ä‘áº¡i
    /// </summary>
    public bool CreateStaticWebApp(string projectName, out string projectDir)
    {
        projectDir = Path.Combine(_wwwRoot, projectName);
        if (Directory.Exists(projectDir))
        {
            OnLog?.Invoke($"Lá»—i: ThÆ° má»¥c dá»± Ã¡n '{projectName}' Ä‘Ã£ tá»“n táº¡i trong www/");
            return false;
        }

        Directory.CreateDirectory(projectDir);

        string htmlContent = $@"<!DOCTYPE html>
<html lang=""vi"">
<head>
    <meta charset=""UTF-8"">
    <meta name=""viewport"" content=""width=device-width, initial-scale=1.0"">
    <title>{projectName} - Static Web App</title>
    <link rel=""stylesheet"" href=""style.css"">
</head>
<body>
    <div class=""hero"">
        <div class=""badge"">Novagon Static App</div>
        <h1>{projectName}</h1>
        <p>Dá»± Ã¡n Frontend HTML5/CSS/JS Ä‘Ã£ sáºµn sÃ ng phÃ¡t triá»ƒn trÃªn domain <strong>{projectName}.test</strong>.</p>
        <button id=""btnAction"" class=""btn"">Nháº¥p vÃ o Ä‘Ã¢y</button>
        <div id=""output"" class=""output""></div>
    </div>
    <script src=""app.js""></script>
</body>
</html>";

        string cssContent = @"* { margin: 0; padding: 0; box-sizing: border-box; }
body { font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, sans-serif; background: #0f172a; color: #f8fafc; display: flex; align-items: center; justify-content: center; min-height: 100vh; padding: 20px; }
.hero { max-width: 600px; text-align: center; background: #1e293b; padding: 40px; border-radius: 12px; border: 1px solid #334155; box-shadow: 0 10px 25px rgba(0,0,0,0.3); }
.badge { display: inline-block; background: #6366f1; color: #ffffff; font-size: 12px; font-weight: bold; padding: 4px 12px; border-radius: 9999px; margin-bottom: 16px; }
h1 { font-size: 32px; font-weight: 800; margin-bottom: 12px; color: #ffffff; }
p { font-size: 15px; color: #94a3b8; margin-bottom: 24px; line-height: 1.6; }
.btn { background: #6366f1; color: #ffffff; border: none; padding: 12px 24px; border-radius: 6px; font-size: 15px; font-weight: bold; cursor: pointer; transition: background 0.2s; }
.btn:hover { background: #4f46e5; }
.output { margin-top: 16px; font-size: 14px; color: #38bdf8; font-weight: bold; }
";

        string jsContent = @"document.getElementById('btnAction').addEventListener('click', function() {
    document.getElementById('output').innerText = 'âœ“ JavaScript hoáº¡t Ä‘á»™ng hoÃ n háº£o! ChÃºc báº¡n xÃ¢y dá»±ng website vui váº».';
});
";

        File.WriteAllText(Path.Combine(projectDir, "index.html"), htmlContent);
        File.WriteAllText(Path.Combine(projectDir, "style.css"), cssContent);
        File.WriteAllText(Path.Combine(projectDir, "app.js"), jsContent);

        OnLog?.Invoke($"[QuickApp] ÄÃ£ táº¡o thÃ nh cÃ´ng dá»± Ã¡n Static Web '{projectName}' táº¡i {projectDir}");
        return true;
    }

    /// <summary>
    /// Táº¡o dá»± Ã¡n WordPress 1-Click (Tá»± táº£i tá»« wordpress.org, giáº£i nÃ©n vÃ  cáº¥u hÃ¬nh toÃ n diá»‡n)
    /// </summary>
    public Task<(bool Success, string Message, string ProjectDir)> CreateWordPressAppAsync(
        string projectName, Action<int>? progressCallback = null)
    {
        return CreateWordPressAppAsync(projectName, createDb: true, progressCallback != null ? (pct, _) => progressCallback(pct) : null);
    }

    public Task<(bool Success, string Message, string ProjectDir)> CreateWordPressAppAsync(
        string projectName, bool createDb, Action<int>? progressCallback)
    {
        return CreateWordPressAppAsync(projectName, createDb, progressCallback != null ? (pct, _) => progressCallback(pct) : null);
    }

    public async Task<(bool Success, string Message, string ProjectDir)> CreateWordPressAppAsync(
        string projectName, bool createDb, Action<int, string>? progressCallback = null)
    {
        string projectDir = Path.Combine(_wwwRoot, projectName);
        if (Directory.Exists(projectDir))
        {
            return (false, $"ThÆ° má»¥c '{projectName}' Ä‘Ã£ tá»“n táº¡i trong www/", projectDir);
        }

        string zipPath = Path.Combine(_cacheDir, "wordpress-latest.zip");
        string dbName = NormalizeDbName(projectName);

        try
        {
            // 1. Táº£i WordPress náº¿u chÆ°a cÃ³ trong cache
            if (!File.Exists(zipPath) || new FileInfo(zipPath).Length < 1024 * 1024)
            {
                progressCallback?.Invoke(5, "Äang káº¿t ná»‘i tá»›i wordpress.org Ä‘á»ƒ táº£i báº£n má»›i nháº¥t...");
                OnLog?.Invoke("Äang táº£i gÃ³i cÃ i Ä‘áº·t WordPress má»›i nháº¥t tá»« wordpress.org...");
                string wpUrl = "https://wordpress.org/latest.zip";

                using var response = await HttpClient.GetAsync(wpUrl, HttpCompletionOption.ResponseHeadersRead);
                response.EnsureSuccessStatusCode();

                long totalBytes = response.Content.Headers.ContentLength ?? 25 * 1024 * 1024;
                await using var stream = await response.Content.ReadAsStreamAsync();
                await using var fileStream = new FileStream(zipPath, FileMode.Create, FileAccess.Write, FileShare.None);

                byte[] buffer = new byte[81920];
                long totalRead = 0;
                int read;

                while ((read = await stream.ReadAsync(buffer, 0, buffer.Length)) > 0)
                {
                    await fileStream.WriteAsync(buffer.AsMemory(0, read));
                    totalRead += read;
                    int percent = (int)(totalRead * 30 / totalBytes); // 5% - 35% cho bÆ°á»›c táº£i
                    progressCallback?.Invoke(5 + percent, $"Äang táº£i WordPress tá»« wordpress.org ({percent * 100 / 30}%)...");
                }
                OnLog?.Invoke("ÄÃ£ táº£i xong gÃ³i WordPress.");
            }
            else
            {
                progressCallback?.Invoke(15, "ÄÃ£ cÃ³ sáºµn gÃ³i WordPress trong mÃ¡y (bá» qua bÆ°á»›c táº£i)...");
            }

            // 2. Giáº£i nÃ©n trá»±c tiáº¿p vÃ o thÆ° má»¥c dá»± Ã¡n (KhÃ´ng dÃ¹ng MoveDirectory Ä‘á»ƒ trÃ¡nh lá»—i Access Denied)
            Directory.CreateDirectory(projectDir);
            progressCallback?.Invoke(25, $"Äang giáº£i nÃ©n mÃ£ nguá»“n WordPress vÃ o www/{projectName}...");
            OnLog?.Invoke($"Äang giáº£i nÃ©n WordPress vÃ o www/{projectName}...");

            using (var archive = ZipFile.OpenRead(zipPath))
            {
                int totalEntries = archive.Entries.Count;
                int extractedCount = 0;

                foreach (var entry in archive.Entries)
                {
                    string relativePath = entry.FullName;
                    if (relativePath.StartsWith("wordpress/", StringComparison.OrdinalIgnoreCase))
                    {
                        relativePath = relativePath.Substring("wordpress/".Length);
                    }
                    else if (relativePath.StartsWith("wordpress\\", StringComparison.OrdinalIgnoreCase))
                    {
                        relativePath = relativePath.Substring("wordpress\\".Length);
                    }

                    if (string.IsNullOrWhiteSpace(relativePath))
                        continue;

                    string destPath = Path.GetFullPath(Path.Combine(projectDir, relativePath));
                    // Báº£o vá»‡ trÃ¡nh path traversal
                    if (!destPath.StartsWith(projectDir, StringComparison.OrdinalIgnoreCase))
                        continue;

                    if (entry.FullName.EndsWith("/") || entry.FullName.EndsWith("\\"))
                    {
                        Directory.CreateDirectory(destPath);
                    }
                    else
                    {
                        string? parent = Path.GetDirectoryName(destPath);
                        if (parent != null && !Directory.Exists(parent))
                        {
                            Directory.CreateDirectory(parent);
                        }
                        entry.ExtractToFile(destPath, overwrite: true);
                    }

                    extractedCount++;
                    if (extractedCount % 250 == 0 || extractedCount == totalEntries)
                    {
                        int pct = 25 + (int)(extractedCount * 45L / Math.Max(totalEntries, 1)); // 25% -> 70%
                        progressCallback?.Invoke(pct, $"Äang giáº£i nÃ©n WordPress ({extractedCount}/{totalEntries} tá»‡p)...");
                    }
                }
            }

            // 3. Tá»± táº¡o Database náº¿u Ä‘Æ°á»£c chá»n (náº¿u lá»—i dá»«ng ngay láº­p tá»©c)
            if (createDb)
            {
                progressCallback?.Invoke(75, $"Äang tá»± Ä‘á»™ng khá»Ÿi táº¡o Database MySQL '{dbName}'...");
                if (!EnsureDatabaseCreated(dbName))
                {
                    string err = $"KhÃ´ng thá»ƒ táº¡o Database '{dbName}'. QuÃ¡ trÃ¬nh cÃ i Ä‘áº·t Ä‘Ã£ dá»«ng láº¡i.";
                    OnLog?.Invoke($"[QuickApp] âœ— {err}");
                    return (false, err, projectDir);
                }
            }

            progressCallback?.Invoke(85, "Äang táº¡o file cáº¥u hÃ¬nh wp-config.php vÃ  cÃ¡c khÃ³a báº£o máº­t...");
            string wpConfigSample = Path.Combine(projectDir, "wp-config-sample.php");
            string wpConfigTarget = Path.Combine(projectDir, "wp-config.php");

            if (File.Exists(wpConfigSample))
            {
                string content = File.ReadAllText(wpConfigSample);
                content = content.Replace("database_name_here", dbName);
                content = content.Replace("username_here", "root");
                content = content.Replace("password_here", "");
                content = content.Replace("localhost", "127.0.0.1");

                // Tá»± sinh 8 Salt Keys ngáº«u nhiÃªn
                string[] saltKeys = { "AUTH_KEY", "SECURE_AUTH_KEY", "LOGGED_IN_KEY", "NONCE_KEY", "AUTH_SALT", "SECURE_AUTH_SALT", "LOGGED_IN_SALT", "NONCE_SALT" };
                foreach (var salt in saltKeys)
                {
                    string targetPlaceholder = $"define( '{salt}',        'put your unique phrase here' );";
                    string targetAltPlaceholder = $"define('{salt}', 'put your unique phrase here');";
                    string randomValue = GenerateRandomKey(64);
                    string replacement = $"define('{salt}', '{randomValue}');";

                    if (content.Contains(targetPlaceholder))
                        content = content.Replace(targetPlaceholder, replacement);
                    else if (content.Contains(targetAltPlaceholder))
                        content = content.Replace(targetAltPlaceholder, replacement);
                }

                // Bá»• sung cÃ¡c cá» cáº¥u hÃ¬nh tá»‘i Æ°u cá»§a Novagon: Direct FS (cÃ i plugin khÃ´ng há»i FTP) vÃ  WP_MEMORY_LIMIT
                var extraConfigs = new StringBuilder();
                extraConfigs.AppendLine();
                extraConfigs.AppendLine("/* Tá»‘i Æ°u hÃ³a bá»Ÿi Novagon Local Environment */");
                extraConfigs.AppendLine("define('FS_METHOD', 'direct');");
                extraConfigs.AppendLine("define('WP_MEMORY_LIMIT', '256M');");
                extraConfigs.AppendLine();
                extraConfigs.AppendLine("/* That's all, stop editing! Happy publishing. */");

                if (content.Contains("/* That's all, stop editing! Happy publishing. */"))
                {
                    content = content.Replace("/* That's all, stop editing! Happy publishing. */", extraConfigs.ToString());
                }
                else
                {
                    content += extraConfigs.ToString();
                }

                File.WriteAllText(wpConfigTarget, content);
            }

            progressCallback?.Invoke(100, "HoÃ n táº¥t khá»Ÿi táº¡o WordPress!");
            OnLog?.Invoke($"[QuickApp] ÄÃ£ táº¡o xong WordPress dá»± Ã¡n '{projectName}' (Database: '{dbName}').");
            return (true, "Khá»Ÿi táº¡o WordPress thÃ nh cÃ´ng!", projectDir);
        }
        catch (Exception ex)
        {
            OnLog?.Invoke($"[QuickApp] Lá»—i táº¡o WordPress: {ex.Message}");
            return (false, $"Lá»—i cÃ i Ä‘áº·t WordPress: {ex.Message}", projectDir);
        }
    }

    private static string GenerateRandomKey(int length = 64)
    {
        const string chars = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789!@#$%^&*()-_ []{}<>~`+=,.;:/?|";
        var bytes = new byte[length];
        RandomNumberGenerator.Fill(bytes);
        var sb = new StringBuilder(length);
        foreach (byte b in bytes)
        {
            sb.Append(chars[b % chars.Length]);
        }
        return sb.ToString().Replace("'", "\\'");
    }
}

