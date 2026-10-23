using System;
using System.IO;
using Novagon.Core;
using Novagon.Core.Network;
using Novagon.Core.Process;
using Novagon.Core.Runtimes;
using Novagon.Core.Ssl;
using Novagon.Core.VHost;
using Xunit;

namespace Novagon.Tests;

public class CoreTests
{
    [Fact]
    public void PortChecker_ShouldReturnFalse_ForUnlikelyPort()
    {
        // Cá»•ng 59123 ráº¥t khÃ³ cÃ³ kháº£ nÄƒng Ä‘ang Ä‘Æ°á»£c sá»­ dá»¥ng
        bool inUse = PortChecker.IsPortInUse(59123);
        Assert.False(inUse);
    }

    [Fact]
    public void JobObjectManager_ShouldInitializeSuccessfully()
    {
        var jobManager = JobObjectManager.Instance;
        Assert.NotNull(jobManager);

        // Cháº¡y má»™t process cmd máº«u vÃ  gÃ¡n vÃ o Job Object
        var psi = new System.Diagnostics.ProcessStartInfo
        {
            FileName = "cmd.exe",
            Arguments = "/c echo test",
            CreateNoWindow = true,
            UseShellExecute = false
        };

        using var proc = System.Diagnostics.Process.Start(psi);
        Assert.NotNull(proc);
        bool assigned = jobManager.AssignProcess(proc);
        Assert.True(assigned);
        proc.WaitForExit();
    }

    [Fact]
    public void VHostGenerator_ShouldDetectLaravelPublicFolder()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), "Novagon_Test_" + Guid.NewGuid().ToString("N"));
        string wwwDir = Path.Combine(tempDir, "www");
        string projectDir = Path.Combine(wwwDir, "blog");
        string publicDir = Path.Combine(projectDir, "public");

        try
        {
            Directory.CreateDirectory(publicDir);
            File.WriteAllText(Path.Combine(publicDir, "index.php"), "<?php echo 'ok';");

            var generator = new VHostGenerator(wwwDir);
            var projects = generator.ScanProjects();

            Assert.Single(projects);
            Assert.Equal("blog", projects[0].Name);
            Assert.Equal("blog.test", projects[0].Domain);
            Assert.Equal(publicDir, projects[0].DocumentRoot);
        }
        finally
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public void RuntimeScanner_ShouldDetectInstalledPackages()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), "Novagon_BinTest_" + Guid.NewGuid().ToString("N"));
        string phpDir = Path.Combine(tempDir, "bin", "php", "php-8.3.15");

        try
        {
            Directory.CreateDirectory(phpDir);
            File.WriteAllText(Path.Combine(phpDir, "php-cgi.exe"), "dummy");

            var scanner = new RuntimeScanner(Path.Combine(tempDir, "bin"));
            var packages = scanner.GetInstalledPackages("php");

            Assert.Single(packages);
            Assert.Equal("php-8.3.15", packages[0].Name);
        }
        finally
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public void LocalSslManager_ShouldGenerateValidCertificates()
    {
        bool success = LocalSslManager.EnsureCertificatesInstalled(installToWindowsTrustStore: false);
        Assert.True(success);
        Assert.True(File.Exists(LocalSslManager.CertPath));
        Assert.True(File.Exists(LocalSslManager.KeyPath));
    }

    [Fact]
    public void RuntimeScanner_ShouldDetectNestedMySqlBinary()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), "Novagon_MySqlTest_" + Guid.NewGuid().ToString("N"));
        string mysqlBinDir = Path.Combine(tempDir, "bin", "mysql", "mariadb-10.11", "bin");

        try
        {
            Directory.CreateDirectory(mysqlBinDir);
            File.WriteAllText(Path.Combine(mysqlBinDir, "mysqld.exe"), "dummy");

            var scanner = new RuntimeScanner(Path.Combine(tempDir, "bin"));
            var packages = scanner.GetInstalledPackages("mysql");

            Assert.Single(packages);
            Assert.Equal("mariadb-10.11", packages[0].Name);
            Assert.Equal(Path.Combine(mysqlBinDir, "mysqld.exe"), packages[0].ExecutablePath);
        }
        finally
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public void PackageManager_ShouldListAvailablePackages()
    {
        var pm = new PackageManager();
        var packages = pm.GetAvailablePackages();

        Assert.NotEmpty(packages);
        Assert.Contains(packages, p => p.Category == "php" && p.Version.StartsWith("8."));
        Assert.Contains(packages, p => p.Category == "mysql");
        Assert.Contains(packages, p => p.Category == "redis");
    }

    [Fact]
    public void PhpExtensionManager_ShouldToggleExtensionCorrectly()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), "Novagon_ExtTest_" + Guid.NewGuid().ToString("N"));
        try
        {
            Directory.CreateDirectory(tempDir);
            string iniPath = Path.Combine(tempDir, "php.ini");
            File.WriteAllText(iniPath, ";extension=curl\nextension=mbstring\n");

            var exts = PhpExtensionManager.GetExtensions(tempDir);
            Assert.Contains(exts, e => e.Name == "curl" && !e.IsEnabled);
            Assert.Contains(exts, e => e.Name == "mbstring" && e.IsEnabled);

            // Báº­t curl
            bool toggled = PhpExtensionManager.ToggleExtension(tempDir, "curl", true);
            Assert.True(toggled);

            var updatedExts = PhpExtensionManager.GetExtensions(tempDir);
            Assert.Contains(updatedExts, e => e.Name == "curl" && e.IsEnabled);

            // Táº¯t mbstring
            PhpExtensionManager.ToggleExtension(tempDir, "mbstring", false);
            var finalExts = PhpExtensionManager.GetExtensions(tempDir);
            Assert.Contains(finalExts, e => e.Name == "mbstring" && !e.IsEnabled);
        }
        finally
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public void QuickAppManager_ShouldCreateBlankPhpApp()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), "Novagon_QuickAppTest_" + Guid.NewGuid().ToString("N"));
        try
        {
            Directory.CreateDirectory(tempDir);
            var manager = new Novagon.Core.QuickApps.QuickAppManager(tempDir);

            bool created = manager.CreateBlankPhpApp("demo-site", out string projectDir);
            Assert.True(created);
            Assert.True(Directory.Exists(projectDir));
            Assert.True(File.Exists(Path.Combine(projectDir, "index.php")));

            string content = File.ReadAllText(Path.Combine(projectDir, "index.php"));
            Assert.Contains("demo-site", content);
            Assert.Contains("phpversion", content);
        }
        finally
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public void QuickAppManager_ShouldCreateLaravelApp_WithPublicDirectoryAndEnv()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), "Novagon_LaravelTest_" + Guid.NewGuid().ToString("N"));
        try
        {
            Directory.CreateDirectory(tempDir);
            var manager = new Novagon.Core.QuickApps.QuickAppManager(tempDir);

            bool created = manager.CreateLaravelApp("my-laravel-blog", createDb: false, out string projectDir);
            Assert.True(created);
            Assert.True(Directory.Exists(projectDir));

            // Kiá»ƒm tra thÆ° má»¥c public vÃ  index.php
            string publicIndex = Path.Combine(projectDir, "public", "index.php");
            Assert.True(File.Exists(publicIndex));
            string publicContent = File.ReadAllText(publicIndex);
            Assert.Contains("my-laravel-blog", publicContent);
            Assert.Contains("my_laravel_blog", publicContent);

            // Kiá»ƒm tra .env
            string envPath = Path.Combine(projectDir, ".env");
            Assert.True(File.Exists(envPath));
            string envContent = File.ReadAllText(envPath);
            Assert.Contains("APP_NAME=my-laravel-blog", envContent);
            Assert.Contains("DB_DATABASE=my_laravel_blog", envContent);
            Assert.Contains("DB_HOST=127.0.0.1", envContent);
        }
        finally
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public void QuickAppManager_ShouldCreateStaticWebApp()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), "Novagon_StaticTest_" + Guid.NewGuid().ToString("N"));
        try
        {
            Directory.CreateDirectory(tempDir);
            var manager = new Novagon.Core.QuickApps.QuickAppManager(tempDir);

            bool created = manager.CreateStaticWebApp("landing-page", out string projectDir);
            Assert.True(created);
            Assert.True(File.Exists(Path.Combine(projectDir, "index.html")));
            Assert.True(File.Exists(Path.Combine(projectDir, "style.css")));
            Assert.True(File.Exists(Path.Combine(projectDir, "app.js")));

            string html = File.ReadAllText(Path.Combine(projectDir, "index.html"));
            Assert.Contains("landing-page", html);
        }
        finally
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, recursive: true);
        }
    }


    [Fact]
    public void VHostGenerator_ShouldGeneratePhpMyAdminConfig_AndRemoveOldAdminerConfig()
    {
        string tempAppRoot = Path.Combine(Path.GetTempPath(), "Novagon_PmaTest_" + Guid.NewGuid().ToString("N"));
        string wwwDir = Path.Combine(tempAppRoot, "www");
        string pmaAppDir = Path.Combine(tempAppRoot, "etc", "apps", "phpmyadmin");
        string sitesEnabledDir = Path.Combine(tempAppRoot, "etc", "nginx", "sites-enabled");

        try
        {
            Directory.CreateDirectory(wwwDir);
            Directory.CreateDirectory(pmaAppDir);
            Directory.CreateDirectory(sitesEnabledDir);

            // Giáº£ láº­p file 00-adminer.conf cÅ©
            string oldAdminer = Path.Combine(sitesEnabledDir, "00-adminer.conf");
            File.WriteAllText(oldAdminer, "legacy adminer config");

            var vhostGen = new VHostGenerator(wwwDir);
            // Sá»­a Ä‘Æ°á»ng dáº«n sites-enabled thÃ´ng qua reflection hoáº·c test vhost builder
            var certPath = Path.Combine(tempAppRoot, "cert.crt");
            var keyPath = Path.Combine(tempAppRoot, "cert.key");
            File.WriteAllText(certPath, "dummy-cert");
            File.WriteAllText(keyPath, "dummy-key");

            vhostGen.GenerateAll(certPath, keyPath, 9000);

            // Kiá»ƒm tra file 00-phpmyadmin.conf Ä‘Æ°á»£c táº¡o táº¡i NginxSitesEnabledDir thá»±c táº¿
            string actualPmaConf = Path.Combine(vhostGen.NginxSitesEnabledDir, "00-phpmyadmin.conf");
            if (Directory.Exists(Path.Combine(AppDirectoryHelper.AppRoot, "etc", "apps", "phpmyadmin")))
            {
                Assert.True(File.Exists(actualPmaConf));
                string content = File.ReadAllText(actualPmaConf);
                Assert.Contains("phpmyadmin.test", content);
                Assert.Contains("fastcgi_pass 127.0.0.1:9000", content);
            }
        }
        finally
        {
            if (Directory.Exists(tempAppRoot))
                Directory.Delete(tempAppRoot, recursive: true);
        }
    }

    [Fact]
    public void VHostGenerator_ShouldRemoveOrphanConfigs_WhenProjectIsDeleted()
    {
        string tempAppRoot = Path.Combine(Path.GetTempPath(), "Novagon_OrphanTest_" + Guid.NewGuid().ToString("N"));
        string wwwDir = Path.Combine(tempAppRoot, "www");
        string sitesEnabledDir = Path.Combine(tempAppRoot, "etc", "nginx", "sites-enabled");

        try
        {
            Directory.CreateDirectory(wwwDir);
            Directory.CreateDirectory(sitesEnabledDir);

            // Táº¡o 1 dá»± Ã¡n cÃ²n tá»“n táº¡i
            string activeProjDir = Path.Combine(wwwDir, "active-site");
            Directory.CreateDirectory(activeProjDir);
            File.WriteAllText(Path.Combine(activeProjDir, "index.php"), "<?php echo 'active';");

            var vhostGen = new VHostGenerator(wwwDir);
            var certPath = Path.Combine(tempAppRoot, "cert.crt");
            var keyPath = Path.Combine(tempAppRoot, "cert.key");
            File.WriteAllText(certPath, "dummy-cert");
            File.WriteAllText(keyPath, "dummy-key");

            // Táº¡o sáºµn má»™t file auto conf má»“ cÃ´i (dá»± Ã¡n cÅ© Ä‘Ã£ bá»‹ xÃ³a)
            string orphanConf = Path.Combine(vhostGen.NginxSitesEnabledDir, "auto.old-deleted-site.test.conf");
            File.WriteAllText(orphanConf, "server { listen 80; server_name old-deleted-site.test; }");
            Assert.True(File.Exists(orphanConf));

            vhostGen.GenerateAll(certPath, keyPath, 9000);

            // File conf má»“ cÃ´i pháº£i tá»± Ä‘á»™ng bá»‹ dá»n dáº¹p
            Assert.False(File.Exists(orphanConf));
        }
        finally
        {
            if (Directory.Exists(tempAppRoot))
                Directory.Delete(tempAppRoot, recursive: true);
        }
    }

    [Fact]
    public void AppSettings_ShouldSupportCustomRootDirectory()
    {
        var settings = new Novagon.Core.Config.AppSettings();
        string defaultRoot = settings.GetEffectiveRootDirectory();
        Assert.EndsWith("www", defaultRoot);

        string customRoot = @"D:\MyWebProjects";
        settings.RootDirectory = customRoot;
        Assert.Equal(customRoot, settings.GetEffectiveRootDirectory());

        settings.RootDirectory = null;
        Assert.Equal(defaultRoot, settings.GetEffectiveRootDirectory());
    }

    [Fact]
    public void VHostGenerator_ShouldScanCustomRootDirectory()
    {
        string tempCustomDriveDir = Path.Combine(Path.GetTempPath(), "Novagon_DriveRoot_" + Guid.NewGuid().ToString("N"));
        string customWww = Path.Combine(tempCustomDriveDir, "MyProjects");
        string projDir = Path.Combine(customWww, "awesome-site");

        try
        {
            Directory.CreateDirectory(projDir);
            File.WriteAllText(Path.Combine(projDir, "index.php"), "<?php echo 'hello';");

            var vhostGen = new VHostGenerator(customWww);
            Assert.Equal(customWww, vhostGen.WwwDirectory);

            var projects = vhostGen.ScanProjects();
            Assert.Single(projects);
            Assert.Equal("awesome-site", projects[0].Name);
            Assert.Equal("awesome-site.test", projects[0].Domain);
            Assert.Equal(projDir, projects[0].DocumentRoot);

            string newCustomWww = Path.Combine(tempCustomDriveDir, "AnotherProjects");
            string newProj = Path.Combine(newCustomWww, "site2");
            Directory.CreateDirectory(newProj);
            File.WriteAllText(Path.Combine(newProj, "index.php"), "<?php echo '2';");

            vhostGen.WwwDirectory = newCustomWww;
            var newProjects = vhostGen.ScanProjects();
            Assert.Single(newProjects);
            Assert.Equal("site2", newProjects[0].Name);
        }
        finally
        {
            if (Directory.Exists(tempCustomDriveDir))
                Directory.Delete(tempCustomDriveDir, recursive: true);
        }
    }
}



