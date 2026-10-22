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
    /// Chuẩn hóa tên database từ tên dự án (ví dụ "my-blog" -> "my_blog")
    /// </summary>
    public static string NormalizeDbName(string projectName)
    {
        return projectName.Replace('-', '_').Replace(' ', '_').ToLowerInvariant();
    }

    /// <summary>
    /// Tự động tạo MySQL/MariaDB database nếu server đang chạy
    /// </summary>
    public bool EnsureDatabaseCreated(string dbName)
    {
        try
        {
            if (!PortChecker.IsPortInUse(3306))
            {
                OnLog?.Invoke($"[DB] Lỗi: MySQL chưa chạy trên cổng 3306. Không thể tạo Database '{dbName}'.");
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
                OnLog?.Invoke($"[DB] Lỗi: Không tìm thấy tệp thực thi mysql.exe trong {mysqlBinDir}");
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
                OnLog?.Invoke($"[DB] Lỗi: Không thể khởi chạy tiến trình mysql.exe");
                return false;
            }

            proc.WaitForExit(5000);
            if (proc.ExitCode == 0)
            {
                OnLog?.Invoke($"[DB] ✓ Đã tự động tạo Database MySQL: '{dbName}'");
                return true;
            }

            string err = proc.StandardError.ReadToEnd().Trim();
            OnLog?.Invoke($"[DB] Lỗi khi tạo Database '{dbName}' (ExitCode: {proc.ExitCode}): {err}");
            return false;
        }
        catch (Exception ex)
        {
            OnLog?.Invoke($"[DB] Ngoại lệ khi tạo Database: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Tạo dự án PHP Blank thuần với giao diện kiểm tra server hiện đại
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
            OnLog?.Invoke($"Lỗi: Thư mục dự án '{projectName}' đã tồn tại trong www/");
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
        <h1>{projectName} <span class=""badge"">Đang hoạt động</span></h1>
        <p>Dự án được khởi tạo thành công bởi <strong>Novagon Quick App</strong>.</p>

        <div class=""info-grid"">
            <div class=""info-box"">
                <div class=""info-title"">Phiên bản PHP</div>
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
                        echo '<span class=""status-ok"">✓ Kết nối thành công</span>';
                    }} catch (Exception $e) {{
                        echo '<span class=""status-err"">Chưa kết nối: ' . htmlspecialchars($e->getMessage()) . '</span>';
                    }}
                    ?>
                </div>
            </div>
        </div>

        <div class=""btn-group"">
            <a href=""?phpinfo=1"" class=""btn"" target=""_blank"">Xem cấu hình PHP Info &rarr;</a>
            <a href=""http://127.0.0.1/phpmyadmin"" class=""btn btn-secondary"" target=""_blank"">Mở phpMyAdmin &rarr;</a>
        </div>
        <?php if (isset($_GET['phpinfo'])) {{ phpinfo(); exit; }} ?>
    </div>
</body>
</html>";

        File.WriteAllText(Path.Combine(projectDir, "index.php"), indexPhpContent);
        OnLog?.Invoke($"[QuickApp] Đã tạo thành công dự án Blank PHP '{projectName}' tại {projectDir}");
        return true;
    }

    /// <summary>
    /// Tìm tệp thực thi php.exe trong bin/php
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
    /// Tìm hoặc kiểm tra composer.phar portable
    /// </summary>
    public static string? FindComposerPhar()
    {
        // 1. Kiểm tra bin/composer/composer.phar trong AppRoot
        string localPhar = Path.Combine(AppDirectoryHelper.AppRoot, "bin", "composer", "composer.phar");
        if (File.Exists(localPhar)) return localPhar;

        // 2. Kiểm tra D:\laragon\bin\composer\composer.phar nếu có
        string altLaragon = @"D:\laragon\bin\composer\composer.phar";
        if (File.Exists(altLaragon)) return altLaragon;

        return null;
    }

    /// <summary>
    /// Tạo dự án Laravel chuẩn 100% bằng Composer (composer create-project laravel/laravel)
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
                return (false, $"Thư mục dự án '{projectName}' đã tồn tại trong www/", projectDir);
            }

            string dbName = NormalizeDbName(projectName);

            // 1. Tự động tạo Database MySQL nếu được chọn
            if (createDb)
            {
                progressCallback?.Invoke(10, $"Đang tạo Database MySQL '{dbName}'...");
                if (!EnsureDatabaseCreated(dbName))
                {
                    string err = $"Không thể tạo Database '{dbName}'. Quá trình cài đặt đã dừng lại.";
                    OnLog?.Invoke($"[QuickApp] ✗ {err}");
                    return (false, err, projectDir);
                }
            }

            // 2. Tìm PHP và Composer
            progressCallback?.Invoke(15, "Đang chuẩn bị môi trường PHP và Composer...");
            string? phpExe = FindPhpExecutable();
            if (phpExe == null)
            {
                return (false, "Không tìm thấy php.exe trong bin/php/", projectDir);
            }

            string? composerPhar = FindComposerPhar();
            if (composerPhar == null)
            {
                progressCallback?.Invoke(20, "Đang tải bộ nạp Composer mới nhất từ getcomposer.org...");
                OnLog?.Invoke("[QuickApp] Đang tải composer.phar từ https://getcomposer.org/download/latest-stable/composer.phar...");
                string composerDir = Path.Combine(AppDirectoryHelper.AppRoot, "bin", "composer");
                Directory.CreateDirectory(composerDir);
                composerPhar = Path.Combine(composerDir, "composer.phar");
                using var response = await HttpClient.GetAsync("https://getcomposer.org/download/latest-stable/composer.phar");
                response.EnsureSuccessStatusCode();
                await using var fs = new FileStream(composerPhar, FileMode.Create, FileAccess.Write, FileShare.None);
                await response.Content.CopyToAsync(fs);
            }

            // 3. Thực thi Composer create-project
            progressCallback?.Invoke(25, "Đang khởi chạy 'composer create-project laravel/laravel'...");
            OnLog?.Invoke($"[Composer] Bắt đầu tạo dự án Laravel: composer create-project laravel/laravel {projectName} --prefer-dist");

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

            // Đưa thư mục chứa php.exe vào biến môi trường PATH để script hậu cài đặt của Composer chạy được php
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
                    // Cập nhật tiến trình ước tính dựa trên các bước của Composer
                    if (e.Data.Contains("Installing dependencies", StringComparison.OrdinalIgnoreCase))
                        progressCallback?.Invoke(40, "Composer: Đang tải các gói dependencies của Laravel...");
                    else if (e.Data.Contains("Generating optimized", StringComparison.OrdinalIgnoreCase))
                        progressCallback?.Invoke(70, "Composer: Đang sinh autoload tối ưu...");
                    else if (e.Data.Contains("artisan", StringComparison.OrdinalIgnoreCase))
                        progressCallback?.Invoke(85, "Composer: Đang khởi tạo ứng dụng qua Artisan...");
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
                OnLog?.Invoke($"[Composer] ✗ Thất bại (ExitCode: {proc.ExitCode}): {err}");
                return (false, $"Lỗi cài đặt Laravel bằng Composer (ExitCode {proc.ExitCode}): {err}", projectDir);
            }

            // 4. Đồng bộ cấu hình Database vào .env của Laravel
            progressCallback?.Invoke(92, "Đang đồng bộ cấu hình Database và Domain vào .env...");
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
                OnLog?.Invoke($"[QuickApp] ✓ Đã cập nhật file .env với DB_DATABASE='{dbName}'");
            }

            progressCallback?.Invoke(100, "Hoàn tất tạo dự án Laravel chuẩn 100%!");
            OnLog?.Invoke($"[QuickApp] ✓ Tạo thành công dự án Laravel '{projectName}' đầy đủ framework và packages.");
            return (true, "Khởi tạo dự án Laravel bằng Composer thành công!", projectDir);
        }
        catch (Exception ex)
        {
            OnLog?.Invoke($"[QuickApp] Lỗi tạo Laravel: {ex.Message}");
            return (false, $"Lỗi cài đặt Laravel: {ex.Message}", projectDir);
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
    /// Tạo khung dự án Laravel Starter đầy đủ chuẩn hóa
    /// </summary>
    public bool CreateLaravelApp(string projectName, bool createDb, out string projectDir)
    {
        projectDir = Path.Combine(_wwwRoot, projectName);
        if (Directory.Exists(projectDir))
        {
            OnLog?.Invoke($"Lỗi: Thư mục dự án '{projectName}' đã tồn tại trong www/");
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

        // Tạo cấu trúc thư mục Laravel
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

        // Sinh APP_KEY ngẫu nhiên
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

        // File public/index.php giao diện chào mừng phong cách Laravel hiện đại
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
            <div>Dự án: <strong>{projectName}</strong></div>
        </div>

        <div class=""card"">
            <div class=""title"">Thông số môi trường cục bộ (Novagon)</div>
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
                            echo '<span class=""ok"">✓ Sẵn sàng kết nối</span>';
                        }} catch (Exception $e) {{
                            echo '<span class=""err"">Chưa sẵn sàng: ' . htmlspecialchars($e->getMessage()) . '</span>';
                        }}
                        ?>
                    </div>
                </div>
            </div>

            <p style=""font-size: 13px; color: #94a3b8; margin-bottom: 18px;"">
                Thư mục <code>public/</code> đã được Novagon tự động nhận diện và cấu hình Virtual Host <code>{projectName}.test</code>.
            </p>
            <a href=""http://127.0.0.1/phpmyadmin"" class=""btn"" target=""_blank"">Quản lý Database trong phpMyAdmin &rarr;</a>
        </div>
    </div>
</body>
</html>";
        File.WriteAllText(Path.Combine(publicDir, "index.php"), publicIndex);

        // File routes/web.php
        string routesWeb = @"<?php
// Routes cho ứng dụng
// Bạn có thể cài đặt Composer và nạp trọn bộ vendor bất cứ lúc nào.
";
        File.WriteAllText(Path.Combine(routesDir, "web.php"), routesWeb);

        OnLog?.Invoke($"[QuickApp] Đã tạo thành công dự án Laravel '{projectName}' tại {projectDir}");
        return true;
    }

    /// <summary>
    /// Tạo dự án Frontend tĩnh HTML5/CSS3/JS hiện đại
    /// </summary>
    public bool CreateStaticWebApp(string projectName, out string projectDir)
    {
        projectDir = Path.Combine(_wwwRoot, projectName);
        if (Directory.Exists(projectDir))
        {
            OnLog?.Invoke($"Lỗi: Thư mục dự án '{projectName}' đã tồn tại trong www/");
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
        <p>Dự án Frontend HTML5/CSS/JS đã sẵn sàng phát triển trên domain <strong>{projectName}.test</strong>.</p>
        <button id=""btnAction"" class=""btn"">Nhấp vào đây</button>
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
    document.getElementById('output').innerText = '✓ JavaScript hoạt động hoàn hảo! Chúc bạn xây dựng website vui vẻ.';
});
";

        File.WriteAllText(Path.Combine(projectDir, "index.html"), htmlContent);
        File.WriteAllText(Path.Combine(projectDir, "style.css"), cssContent);
        File.WriteAllText(Path.Combine(projectDir, "app.js"), jsContent);

        OnLog?.Invoke($"[QuickApp] Đã tạo thành công dự án Static Web '{projectName}' tại {projectDir}");
        return true;
    }

    /// <summary>
    /// Tạo dự án WordPress 1-Click (Tự tải từ wordpress.org, giải nén và cấu hình toàn diện)
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
            return (false, $"Thư mục '{projectName}' đã tồn tại trong www/", projectDir);
        }

        string zipPath = Path.Combine(_cacheDir, "wordpress-latest.zip");
        string dbName = NormalizeDbName(projectName);

        try
        {
            // 1. Tải WordPress nếu chưa có trong cache
            if (!File.Exists(zipPath) || new FileInfo(zipPath).Length < 1024 * 1024)
            {
                progressCallback?.Invoke(5, "Đang kết nối tới wordpress.org để tải bản mới nhất...");
                OnLog?.Invoke("Đang tải gói cài đặt WordPress mới nhất từ wordpress.org...");
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
                    int percent = (int)(totalRead * 30 / totalBytes); // 5% - 35% cho bước tải
                    progressCallback?.Invoke(5 + percent, $"Đang tải WordPress từ wordpress.org ({percent * 100 / 30}%)...");
                }
                OnLog?.Invoke("Đã tải xong gói WordPress.");
            }
            else
            {
                progressCallback?.Invoke(15, "Đã có sẵn gói WordPress trong máy (bỏ qua bước tải)...");
            }

            // 2. Giải nén trực tiếp vào thư mục dự án (Không dùng MoveDirectory để tránh lỗi Access Denied)
            Directory.CreateDirectory(projectDir);
            progressCallback?.Invoke(25, $"Đang giải nén mã nguồn WordPress vào www/{projectName}...");
            OnLog?.Invoke($"Đang giải nén WordPress vào www/{projectName}...");

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
                    // Bảo vệ tránh path traversal
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
                        progressCallback?.Invoke(pct, $"Đang giải nén WordPress ({extractedCount}/{totalEntries} tệp)...");
                    }
                }
            }

            // 3. Tự tạo Database nếu được chọn (nếu lỗi dừng ngay lập tức)
            if (createDb)
            {
                progressCallback?.Invoke(75, $"Đang tự động khởi tạo Database MySQL '{dbName}'...");
                if (!EnsureDatabaseCreated(dbName))
                {
                    string err = $"Không thể tạo Database '{dbName}'. Quá trình cài đặt đã dừng lại.";
                    OnLog?.Invoke($"[QuickApp] ✗ {err}");
                    return (false, err, projectDir);
                }
            }

            progressCallback?.Invoke(85, "Đang tạo file cấu hình wp-config.php và các khóa bảo mật...");
            string wpConfigSample = Path.Combine(projectDir, "wp-config-sample.php");
            string wpConfigTarget = Path.Combine(projectDir, "wp-config.php");

            if (File.Exists(wpConfigSample))
            {
                string content = File.ReadAllText(wpConfigSample);
                content = content.Replace("database_name_here", dbName);
                content = content.Replace("username_here", "root");
                content = content.Replace("password_here", "");
                content = content.Replace("localhost", "127.0.0.1");

                // Tự sinh 8 Salt Keys ngẫu nhiên
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

                // Bổ sung các cờ cấu hình tối ưu của Novagon: Direct FS (cài plugin không hỏi FTP) và WP_MEMORY_LIMIT
                var extraConfigs = new StringBuilder();
                extraConfigs.AppendLine();
                extraConfigs.AppendLine("/* Tối ưu hóa bởi Novagon Local Environment */");
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

            progressCallback?.Invoke(100, "Hoàn tất khởi tạo WordPress!");
            OnLog?.Invoke($"[QuickApp] Đã tạo xong WordPress dự án '{projectName}' (Database: '{dbName}').");
            return (true, "Khởi tạo WordPress thành công!", projectDir);
        }
        catch (Exception ex)
        {
            OnLog?.Invoke($"[QuickApp] Lỗi tạo WordPress: {ex.Message}");
            return (false, $"Lỗi cài đặt WordPress: {ex.Message}", projectDir);
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
