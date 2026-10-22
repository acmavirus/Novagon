using System;
using System.Collections.Generic;
using System.IO;
using Novagon.Core.Hosts;
using Novagon.Core.Network;
using Novagon.Core.Process;
using Novagon.Core.Runtimes;
using Novagon.Core.Ssl;
using Novagon.Core.VHost;

namespace Novagon.Core.Services;

public class ServiceManager : IDisposable
{
    public RuntimeScanner Scanner { get; }
    public VHostGenerator VHostGen { get; }

    public RuntimePackage? ActivePhp { get; set; }
    public RuntimePackage? ActiveMySql { get; set; }
    public string ActiveWebServer { get; set; } = "nginx"; // "nginx" hoặc "apache"

    private ProcessRunner? _webRunner;
    private ProcessRunner? _phpRunner;
    private ProcessRunner? _mysqlRunner;
    private ProcessRunner? _redisRunner;
    private ProcessRunner? _mailpitRunner;

    public RuntimePackage? ActiveRedis { get; set; }

    public ProcessStatus WebStatus => _webRunner?.Status ?? ProcessStatus.Stopped;
    public ProcessStatus PhpStatus => _phpRunner?.Status ?? ProcessStatus.Stopped;
    public ProcessStatus MySqlStatus => _mysqlRunner?.Status ?? ProcessStatus.Stopped;
    public ProcessStatus RedisStatus => _redisRunner?.Status ?? ProcessStatus.Stopped;
    public ProcessStatus MailpitStatus => _mailpitRunner?.Status ?? ProcessStatus.Stopped;

    public bool IsRunning => WebStatus == ProcessStatus.Running || PhpStatus == ProcessStatus.Running || MySqlStatus == ProcessStatus.Running || RedisStatus == ProcessStatus.Running || MailpitStatus == ProcessStatus.Running;

    public event Action<string, string>? OnLogReceived;
    public event Action? OnStateChanged;

    public ServiceManager(string? baseDir = null)
    {
        string root = baseDir ?? AppDirectoryHelper.AppRoot;
        Scanner = new RuntimeScanner(Path.Combine(root, "bin"));
        VHostGen = new VHostGenerator(Path.Combine(root, "www"));

        // Tự động nhận diện phiên bản runtime khả dụng đầu tiên
        var phps = Scanner.GetInstalledPackages("php");
        if (phps.Count > 0) ActivePhp = phps[0];

        var mysqls = Scanner.GetInstalledPackages("mysql");
        if (mysqls.Count > 0) ActiveMySql = mysqls[0];
    }

    public (bool Success, string Message) StartAll()
    {
        Log("System", "Bắt đầu khởi động các dịch vụ...");

        // Nạp lại các package nếu mới được tải vào bin/
        if (ActivePhp == null)
        {
            var phps = Scanner.GetInstalledPackages("php");
            if (phps.Count > 0) ActivePhp = phps[0];
        }
        if (ActiveMySql == null)
        {
            var mysqls = Scanner.GetInstalledPackages("mysql");
            if (mysqls.Count > 0) ActiveMySql = mysqls[0];
        }

        // 1. Kiểm tra xung đột cổng
        if (PortChecker.IsPortInUse(80))
        {
            Log("System", "Cảnh báo: Cổng 80 đang bị ứng dụng khác chiếm giữ (kiểm tra IIS, Skype, v.v.)");
        }
        if (PortChecker.IsPortInUse(3306))
        {
            Log("System", "Cảnh báo: Cổng 3306 (MySQL) đang bị ứng dụng khác chiếm giữ.");
        }

        // 2. Đảm bảo chứng chỉ SSL cục bộ sẵn sàng
        LocalSslManager.EnsureCertificatesInstalled(installToWindowsTrustStore: true);

        // 3. Tự sinh Virtual Hosts và cập nhật file hosts
        try
        {
            if (!HostsFileManager.CanWriteHostsFile())
            {
                Log("VHost", "Lưu ý: Ứng dụng đang chạy quyền User thường. Cần quyền Administrator để tự động sửa file hosts.");
            }
            VHostGen.GenerateAll(LocalSslManager.CertPath, LocalSslManager.KeyPath, phpFastCgiPort: 9000);
            Log("VHost", "Đã quét và đồng bộ các Virtual Hosts.");
        }
        catch (Exception ex)
        {
            Log("VHost", $"Lỗi tạo Virtual Hosts: {ex.Message}");
        }

        // 4. Khởi động PHP FastCGI (nếu có runtime)
        Directory.CreateDirectory(Path.Combine(AppDirectoryHelper.AppRoot, "logs"));

        if (ActivePhp != null && File.Exists(ActivePhp.ExecutablePath))
        {
            string phpIni = Path.Combine(ActivePhp.RootPath, "php.ini");
            string phpArgs = $"-b 127.0.0.1:9000" + (File.Exists(phpIni) ? $" -c \"{phpIni}\"" : "");

            _phpRunner = new ProcessRunner("PHP", ActivePhp.ExecutablePath, phpArgs, ActivePhp.RootPath);
            _phpRunner.EnvironmentVariables["PHP_FCGI_MAX_REQUESTS"] = "1000";
            _phpRunner.OutputDataReceived += msg => Log("PHP", msg);
            _phpRunner.ErrorDataReceived += msg => Log("PHP", msg);
            _phpRunner.StatusChanged += _ => OnStateChanged?.Invoke();
            _phpRunner.Start();
            Log("PHP", $"Khởi động {ActivePhp.Name} trên cổng 9000.");
        }
        else
        {
            Log("PHP", "Không tìm thấy phiên bản PHP nào trong bin/php.");
        }

        // 5. Khởi động Web Server (Nginx)
        var nginxPackages = Scanner.GetInstalledPackages("nginx");
        if (nginxPackages.Count > 0)
        {
            var nginx = nginxPackages[0];
            string nginxConf = Path.Combine(AppDirectoryHelper.AppRoot, "etc", "nginx", "nginx.conf");
            string nginxArgs = $"-p \"{nginx.RootPath}\"" + (File.Exists(nginxConf) ? $" -c \"{nginxConf}\"" : "");

            _webRunner = new ProcessRunner("Nginx", nginx.ExecutablePath, nginxArgs, nginx.RootPath);
            _webRunner.OutputDataReceived += msg => Log("Nginx", msg);
            _webRunner.ErrorDataReceived += msg => Log("Nginx", msg);
            _webRunner.StatusChanged += _ => OnStateChanged?.Invoke();
            _webRunner.Start();
            Log("Nginx", $"Khởi động {nginx.Name}.");
        }
        else
        {
            Log("Nginx", "Không tìm thấy Nginx trong bin/nginx.");
        }

        // 6. Khởi động MySQL / MariaDB (nếu có)
        if (ActiveMySql != null && File.Exists(ActiveMySql.ExecutablePath))
        {
            string myIni = Path.Combine(ActiveMySql.RootPath, "my.ini");
            string dataDir = Path.Combine(ActiveMySql.RootPath, "data");
            string normData = dataDir.Replace('\\', '/');

            // Tự động khởi tạo database hệ thống nếu thư mục data/mysql chưa tồn tại
            string mysqlSystemDir = Path.Combine(dataDir, "mysql");
            if (!Directory.Exists(mysqlSystemDir))
            {
                Log("MySQL", "Chưa phát hiện database hệ thống, đang tự động khởi tạo (bootstrap)...");
                Directory.CreateDirectory(dataDir);

                string binDir = Path.GetDirectoryName(ActiveMySql.ExecutablePath) ?? ActiveMySql.RootPath;
                string installDbExe = Path.Combine(binDir, "mariadb-install-db.exe");
                if (!File.Exists(installDbExe))
                {
                    installDbExe = Path.Combine(binDir, "mysql_install_db.exe");
                }

                if (File.Exists(installDbExe))
                {
                    try
                    {
                        var initPsi = new System.Diagnostics.ProcessStartInfo
                        {
                            FileName = installDbExe,
                            Arguments = $"--datadir=\"{normData}\"",
                            WorkingDirectory = ActiveMySql.RootPath,
                            CreateNoWindow = true,
                            UseShellExecute = false
                        };
                        using var initProc = System.Diagnostics.Process.Start(initPsi);
                        initProc?.WaitForExit(30000);
                        Log("MySQL", "Khởi tạo database hệ thống thành công.");
                    }
                    catch (Exception ex)
                    {
                        Log("MySQL", $"Lỗi khi khởi tạo database: {ex.Message}");
                    }
                }
            }

            if (!File.Exists(myIni))
            {
                string defaultIni = $@"[mysqld]
datadir={normData}
port=3306
bind-address=127.0.0.1
character-set-server=utf8mb4
collation-server=utf8mb4_unicode_ci
default-storage-engine=INNODB
max_allowed_packet=64M
";
                try { File.WriteAllText(myIni, defaultIni); } catch { }
            }

            // Lưu ý: Đối với MySQL/MariaDB, --defaults-file BẮT BUỘC phải là tham số đầu tiên
            string myArgs = File.Exists(myIni)
                ? $"--defaults-file=\"{myIni}\" --standalone --console"
                : "--standalone --console";

            _mysqlRunner = new ProcessRunner("MySQL", ActiveMySql.ExecutablePath, myArgs, ActiveMySql.RootPath);
            _mysqlRunner.OutputDataReceived += msg => Log("MySQL", msg);
            _mysqlRunner.ErrorDataReceived += msg => Log("MySQL", msg);
            _mysqlRunner.StatusChanged += _ => OnStateChanged?.Invoke();
            _mysqlRunner.Start();
            Log("MySQL", $"Khởi động {ActiveMySql.Name} trên cổng 3306.");
        }

        // 7. Khởi động Redis (nếu có trong bin/redis)
        var redisPackages = Scanner.GetInstalledPackages("redis");
        if (redisPackages.Count > 0 && File.Exists(redisPackages[0].ExecutablePath))
        {
            ActiveRedis = redisPackages[0];
            string redisConf = Path.Combine(ActiveRedis.RootPath, "redis.windows.conf");
            string redisArgs = File.Exists(redisConf) ? $"\"{redisConf}\"" : "";

            _redisRunner = new ProcessRunner("Redis", ActiveRedis.ExecutablePath, redisArgs, ActiveRedis.RootPath);
            _redisRunner.OutputDataReceived += msg => Log("Redis", msg);
            _redisRunner.ErrorDataReceived += msg => Log("Redis", msg);
            _redisRunner.StatusChanged += _ => OnStateChanged?.Invoke();
            _redisRunner.Start();
            Log("Redis", $"Khởi động {ActiveRedis.Name} trên cổng 6379.");
        }

        // 8. Khởi động Mailpit (nếu có trong bin/tools/mailpit/mailpit.exe)
        string mailpitExe = GetMailpitPath();
        if (File.Exists(mailpitExe))
        {
            string mailpitDir = Path.GetDirectoryName(mailpitExe)!;
            string mailpitArgs = "--smtp-addr 127.0.0.1:1025 --listen 127.0.0.1:8025";
            _mailpitRunner = new ProcessRunner("Mailpit", mailpitExe, mailpitArgs, mailpitDir);
            _mailpitRunner.OutputDataReceived += msg => Log("Mailpit", msg);
            _mailpitRunner.ErrorDataReceived += msg => Log("Mailpit", msg);
            _mailpitRunner.StatusChanged += _ => OnStateChanged?.Invoke();
            _mailpitRunner.Start();
            Log("Mailpit", "Khởi động Mailpit (SMTP: 127.0.0.1:1025, Web UI: http://127.0.0.1:8025).");
        }

        OnStateChanged?.Invoke();
        return (true, "Đã khởi động các dịch vụ hoàn tất.");
    }

    private bool _isStopping;

    public void StopAll()
    {
        if (_isStopping) return;
        _isStopping = true;

        try
        {
            Log("System", "Đang dừng toàn bộ dịch vụ...");

            // 1. Dừng Nginx một cách lịch sự trước
            var nginxPackages = Scanner.GetInstalledPackages("nginx");
            if (nginxPackages.Count > 0 && File.Exists(nginxPackages[0].ExecutablePath))
            {
                try
                {
                    var stopPsi = new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = nginxPackages[0].ExecutablePath,
                        Arguments = "-s stop",
                        WorkingDirectory = nginxPackages[0].RootPath,
                        CreateNoWindow = true,
                        UseShellExecute = false
                    };
                    using var stopProc = System.Diagnostics.Process.Start(stopPsi);
                    stopProc?.WaitForExit(500);
                }
                catch { }
            }

            // 2. Dừng các ProcessRunner
            _webRunner?.Stop();
            _phpRunner?.Stop();
            _mysqlRunner?.Stop();
            _redisRunner?.Stop();
            _mailpitRunner?.Stop();

            try { _webRunner?.Dispose(); } catch { }
            try { _phpRunner?.Dispose(); } catch { }
            try { _mysqlRunner?.Dispose(); } catch { }
            try { _redisRunner?.Dispose(); } catch { }
            try { _mailpitRunner?.Dispose(); } catch { }

            _webRunner = null;
            _phpRunner = null;
            _mysqlRunner = null;
            _redisRunner = null;
            _mailpitRunner = null;

            Log("System", "Toàn bộ dịch vụ đã dừng.");
            try
            {
                OnStateChanged?.Invoke();
            }
            catch { }
        }
        finally
        {
            _isStopping = false;
        }
    }

    public void ReloadVHosts()
    {
        Log("VHost", "Đang tải lại cấu hình Virtual Hosts...");
        VHostGen.GenerateAll(LocalSslManager.CertPath, LocalSslManager.KeyPath, phpFastCgiPort: 9000);

        // Gửi lệnh reload cho Nginx nếu đang chạy
        var nginxPackages = Scanner.GetInstalledPackages("nginx");
        if (nginxPackages.Count > 0 && _webRunner?.Status == ProcessStatus.Running)
        {
            var psi = new System.Diagnostics.ProcessStartInfo
            {
                FileName = nginxPackages[0].ExecutablePath,
                Arguments = "-s reload",
                WorkingDirectory = nginxPackages[0].RootPath,
                CreateNoWindow = true,
                UseShellExecute = false
            };
            try
            {
                using var p = System.Diagnostics.Process.Start(psi);
                p?.WaitForExit(3000);
            }
            catch { }
        }

        Log("VHost", "Tải lại Virtual Hosts hoàn tất.");
    }

    private void Log(string source, string message)
    {
        OnLogReceived?.Invoke(source, $"[{DateTime.Now:HH:mm:ss}] [{source}] {message}");
    }

    public void Dispose()
    {
        StopAll();
        GC.SuppressFinalize(this);
    }

    public static string GetMailpitPath()
    {
        return Path.Combine(AppDirectoryHelper.AppRoot, "bin", "tools", "mailpit", "mailpit.exe");
    }
}
