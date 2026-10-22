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
    public string ActiveWebServer { get; set; } = "nginx"; // "nginx" hoáº·c "apache"

    private ProcessRunner? _webRunner;
    private ProcessRunner? _phpRunner;
    private ProcessRunner? _mysqlRunner;
    private ProcessRunner? _redisRunner;

    public RuntimePackage? ActiveRedis { get; set; }

    public ProcessStatus WebStatus => _webRunner?.Status ?? ProcessStatus.Stopped;
    public ProcessStatus PhpStatus => _phpRunner?.Status ?? ProcessStatus.Stopped;
    public ProcessStatus MySqlStatus => _mysqlRunner?.Status ?? ProcessStatus.Stopped;
    public ProcessStatus RedisStatus => _redisRunner?.Status ?? ProcessStatus.Stopped;

    public bool IsRunning => WebStatus == ProcessStatus.Running || PhpStatus == ProcessStatus.Running || MySqlStatus == ProcessStatus.Running || RedisStatus == ProcessStatus.Running;

    public event Action<string, string>? OnLogReceived;
    public event Action? OnStateChanged;

    public ServiceManager(string? baseDir = null)
    {
        string root = baseDir ?? AppDirectoryHelper.AppRoot;
        Scanner = new RuntimeScanner(Path.Combine(root, "bin"));
        VHostGen = new VHostGenerator(Path.Combine(root, "www"));

        // Tá»± Ä‘á»™ng nháº­n diá»‡n phiÃªn báº£n runtime kháº£ dá»¥ng Ä‘áº§u tiÃªn
        var phps = Scanner.GetInstalledPackages("php");
        if (phps.Count > 0) ActivePhp = phps[0];

        var mysqls = Scanner.GetInstalledPackages("mysql");
        if (mysqls.Count > 0) ActiveMySql = mysqls[0];
    }

    public (bool Success, string Message) StartAll()
    {
        Log("System", "Báº¯t Ä‘áº§u khá»Ÿi Ä‘á»™ng cÃ¡c dá»‹ch vá»¥...");

        // Náº¡p láº¡i cÃ¡c package náº¿u má»›i Ä‘Æ°á»£c táº£i vÃ o bin/
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

        // 1. Kiá»ƒm tra xung Ä‘á»™t cá»•ng
        if (PortChecker.IsPortInUse(80))
        {
            Log("System", "Cáº£nh bÃ¡o: Cá»•ng 80 Ä‘ang bá»‹ á»©ng dá»¥ng khÃ¡c chiáº¿m giá»¯ (kiá»ƒm tra IIS, Skype, v.v.)");
        }
        if (PortChecker.IsPortInUse(3306))
        {
            Log("System", "Cáº£nh bÃ¡o: Cá»•ng 3306 (MySQL) Ä‘ang bá»‹ á»©ng dá»¥ng khÃ¡c chiáº¿m giá»¯.");
        }

        // 2. Äáº£m báº£o chá»©ng chá»‰ SSL cá»¥c bá»™ sáºµn sÃ ng
        LocalSslManager.EnsureCertificatesInstalled(installToWindowsTrustStore: true);

        // 3. Tá»± sinh Virtual Hosts vÃ  cáº­p nháº­t file hosts
        try
        {
            if (!HostsFileManager.CanWriteHostsFile())
            {
                Log("VHost", "LÆ°u Ã½: á»¨ng dá»¥ng Ä‘ang cháº¡y quyá»n User thÆ°á»ng. Cáº§n quyá»n Administrator Ä‘á»ƒ tá»± Ä‘á»™ng sá»­a file hosts.");
            }
            VHostGen.GenerateAll(LocalSslManager.CertPath, LocalSslManager.KeyPath, phpFastCgiPort: 9000);
            Log("VHost", "ÄÃ£ quÃ©t vÃ  Ä‘á»“ng bá»™ cÃ¡c Virtual Hosts.");
        }
        catch (Exception ex)
        {
            Log("VHost", $"Lá»—i táº¡o Virtual Hosts: {ex.Message}");
        }

        // 4. Khá»Ÿi Ä‘á»™ng PHP FastCGI (náº¿u cÃ³ runtime)
        // Äáº£m báº£o thÆ° má»¥c logs tá»“n táº¡i
        Directory.CreateDirectory(Path.Combine(AppDirectoryHelper.AppRoot, "logs"));

        // 4. Khá»Ÿi Ä‘á»™ng PHP FastCGI (náº¿u cÃ³ runtime)
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
            Log("PHP", $"Khá»Ÿi Ä‘á»™ng {ActivePhp.Name} trÃªn cá»•ng 9000.");
        }
        else
        {
            Log("PHP", "KhÃ´ng tÃ¬m tháº¥y phiÃªn báº£n PHP nÃ o trong bin/php.");
        }

        // 5. Khá»Ÿi Ä‘á»™ng Web Server (Nginx)
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
            Log("Nginx", $"Khá»Ÿi Ä‘á»™ng {nginx.Name}.");
        }
        else
        {
            Log("Nginx", "KhÃ´ng tÃ¬m tháº¥y Nginx trong bin/nginx.");
        }

        // 6. Khá»Ÿi Ä‘á»™ng MySQL / MariaDB (náº¿u cÃ³)
        if (ActiveMySql != null && File.Exists(ActiveMySql.ExecutablePath))
        {
            string myIni = Path.Combine(ActiveMySql.RootPath, "my.ini");
            string dataDir = Path.Combine(ActiveMySql.RootPath, "data");
            string normData = dataDir.Replace('\\', '/');

            // Tá»± Ä‘á»™ng khá»Ÿi táº¡o database há»‡ thá»‘ng náº¿u thÆ° má»¥c data/mysql chÆ°a tá»“n táº¡i
            string mysqlSystemDir = Path.Combine(dataDir, "mysql");
            if (!Directory.Exists(mysqlSystemDir))
            {
                Log("MySQL", "ChÆ°a phÃ¡t hiá»‡n database há»‡ thá»‘ng, Ä‘ang tá»± Ä‘á»™ng khá»Ÿi táº¡o (bootstrap)...");
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
                        Log("MySQL", "Khá»Ÿi táº¡o database há»‡ thá»‘ng thÃ nh cÃ´ng.");
                    }
                    catch (Exception ex)
                    {
                        Log("MySQL", $"Lá»—i khi khá»Ÿi táº¡o database: {ex.Message}");
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

            // LÆ°u Ã½: Äá»‘i vá»›i MySQL/MariaDB, --defaults-file Báº®T BUá»˜C pháº£i lÃ  tham sá»‘ Ä‘áº§u tiÃªn
            string myArgs = File.Exists(myIni)
                ? $"--defaults-file=\"{myIni}\" --standalone --console"
                : "--standalone --console";

            _mysqlRunner = new ProcessRunner("MySQL", ActiveMySql.ExecutablePath, myArgs, ActiveMySql.RootPath);
            _mysqlRunner.OutputDataReceived += msg => Log("MySQL", msg);
            _mysqlRunner.ErrorDataReceived += msg => Log("MySQL", msg);
            _mysqlRunner.StatusChanged += _ => OnStateChanged?.Invoke();
            _mysqlRunner.Start();
            Log("MySQL", $"Khá»Ÿi Ä‘á»™ng {ActiveMySql.Name} trÃªn cá»•ng 3306.");
        }

        // 7. Khá»Ÿi Ä‘á»™ng Redis (náº¿u cÃ³ trong bin/redis)
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
            Log("Redis", $"Khá»Ÿi Ä‘á»™ng {ActiveRedis.Name} trÃªn cá»•ng 6379.");
        }

        OnStateChanged?.Invoke();
        return (true, "ÄÃ£ khá»Ÿi Ä‘á»™ng cÃ¡c dá»‹ch vá»¥ hoÃ n táº¥t.");
    }

    private bool _isStopping;

    public void StopAll()
    {
        if (_isStopping) return;
        _isStopping = true;

        try
        {
            Log("System", "Äang dá»«ng toÃ n bá»™ dá»‹ch vá»¥...");

            // 1. Dá»«ng Nginx má»™t cÃ¡ch lá»‹ch sá»± trÆ°á»›c
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

            // 2. Dá»«ng cÃ¡c ProcessRunner
            _webRunner?.Stop();
            _phpRunner?.Stop();
            _mysqlRunner?.Stop();
            _redisRunner?.Stop();

            try { _webRunner?.Dispose(); } catch { }
            try { _phpRunner?.Dispose(); } catch { }
            try { _mysqlRunner?.Dispose(); } catch { }
            try { _redisRunner?.Dispose(); } catch { }

            _webRunner = null;
            _phpRunner = null;
            _mysqlRunner = null;
            _redisRunner = null;

            Log("System", "ToÃ n bá»™ dá»‹ch vá»¥ Ä‘Ã£ dá»«ng.");
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
        Log("VHost", "Äang táº£i láº¡i cáº¥u hÃ¬nh Virtual Hosts...");
        VHostGen.GenerateAll(LocalSslManager.CertPath, LocalSslManager.KeyPath, phpFastCgiPort: 9000);

        // Gá»­i lá»‡nh reload cho Nginx náº¿u Ä‘ang cháº¡y
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

        Log("VHost", "Táº£i láº¡i Virtual Hosts hoÃ n táº¥t.");
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
}

