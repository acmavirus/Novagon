using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Novagon.Core.Hosts;

namespace Novagon.Core.VHost;

public record ProjectInfo(
    string Name,
    string Domain,
    string ProjectPath,
    string DocumentRoot
);

/// <summary>
/// QuÃ©t thÆ° má»¥c www/ vÃ  tá»± Ä‘á»™ng sinh Virtual Hosts cho Nginx/Apache cÃ¹ng báº£n ghi hosts.
/// </summary>
public class VHostGenerator
{
    public string WwwDirectory { get; }
    public string Tld { get; set; } = "test";
    public string NginxSitesEnabledDir { get; }

    public VHostGenerator(string? wwwDirectory = null)
    {
        WwwDirectory = wwwDirectory ?? Path.Combine(AppDirectoryHelper.AppRoot, "www");
        NginxSitesEnabledDir = Path.Combine(AppDirectoryHelper.AppRoot, "etc", "nginx", "sites-enabled");
    }

    /// <summary>
    /// QuÃ©t táº¥t cáº£ thÆ° má»¥c dá»± Ã¡n trong www/
    /// </summary>
    public List<ProjectInfo> ScanProjects()
    {
        var projects = new List<ProjectInfo>();

        if (!Directory.Exists(WwwDirectory))
        {
            Directory.CreateDirectory(WwwDirectory);
            return projects;
        }

        foreach (var dir in Directory.GetDirectories(WwwDirectory))
        {
            string dirName = Path.GetFileName(dir);
            if (dirName.StartsWith('.') || dirName.StartsWith('_'))
                continue;

            string domain = $"{dirName.ToLowerInvariant()}.{Tld}";

            // Tá»± Ä‘á»™ng nháº­n diá»‡n thÆ° má»¥c public (Laravel/Symfony) hoáº·c dist
            string docRoot = dir;
            string publicDir = Path.Combine(dir, "public");
            string distDir = Path.Combine(dir, "dist");

            if (Directory.Exists(publicDir))
                docRoot = publicDir;
            else if (Directory.Exists(distDir))
                docRoot = distDir;

            projects.Add(new ProjectInfo(dirName, domain, dir, docRoot));
        }

        return projects;
    }

    /// <summary>
    /// Táº¡o cáº¥u hÃ¬nh Nginx VirtualHost vÃ  cáº­p nháº­t file hosts cho táº¥t cáº£ dá»± Ã¡n.
    /// </summary>
    public void GenerateAll(string sslCertPath, string sslKeyPath, int phpFastCgiPort = 9000)
    {
        var projects = ScanProjects();
        Directory.CreateDirectory(NginxSitesEnabledDir);

        var domains = new List<string> { $"localhost.{Tld}", "phpmyadmin.test" };

        // XÃ³a file cáº¥u hÃ¬nh adminer cÅ© náº¿u cÃ³
        string oldAdminerConf = Path.Combine(NginxSitesEnabledDir, "00-adminer.conf");
        if (File.Exists(oldAdminerConf))
        {
            try { File.Delete(oldAdminerConf); } catch { }
        }

        // 1. Sinh cáº¥u hÃ¬nh Default Server (00-default.conf) Ä‘á»ƒ lÃ m default_server chuáº©n cho localhost/127.0.0.1
        // NgÄƒn cháº·n cÃ¡c request tÃªn miá»n dá»± Ã¡n má»›i táº¡o bá»‹ Nginx Ä‘iá»u hÆ°á»›ng nháº§m vÃ o phpMyAdmin
        string defaultConfPath = Path.Combine(NginxSitesEnabledDir, "00-default.conf");
        string normWww = WwwDirectory.Replace('\\', '/');
        string normCert = sslCertPath.Replace('\\', '/');
        string normKey = sslKeyPath.Replace('\\', '/');
        string appsDir = Path.Combine(AppDirectoryHelper.AppRoot, "etc", "apps", "phpmyadmin");
        string normApps = appsDir.Replace('\\', '/');

        string defaultConf = $@"server {{
    listen 80 default_server;
    listen 443 ssl default_server;
    server_name localhost 127.0.0.1 """";

    root ""{normWww}"";
    index index.php index.html index.htm;

    ssl_certificate ""{normCert}"";
    ssl_certificate_key ""{normKey}"";

    location / {{
        try_files $uri $uri/ /index.php?$query_string;
    }}

    location /phpmyadmin/ {{
        alias ""{normApps}/"";
        index index.php index.html;
        try_files $uri $uri/ /phpmyadmin/index.php?$query_string;

        location ~ \.php$ {{
            fastcgi_pass 127.0.0.1:{phpFastCgiPort};
            fastcgi_index index.php;
            fastcgi_param SCRIPT_FILENAME $request_filename;
            include fastcgi_params;
        }}
    }}

    location = /phpmyadmin {{
        return 301 /phpmyadmin/;
    }}

    location ~ \.php$ {{
        fastcgi_pass 127.0.0.1:{phpFastCgiPort};
        fastcgi_index index.php;
        fastcgi_param SCRIPT_FILENAME $document_root$fastcgi_script_name;
        include fastcgi_params;
    }}
}}
";
        File.WriteAllText(defaultConfPath, defaultConf);

        // 2. Sinh cáº¥u hÃ¬nh VHost riÃªng biá»‡t cho phpMyAdmin Database Manager (chá»‰ match phpmyadmin.test)
        if (Directory.Exists(appsDir))
        {
            string phpmyadminConfPath = Path.Combine(NginxSitesEnabledDir, "00-phpmyadmin.conf");
            var pmaProject = new ProjectInfo("phpmyadmin", "phpmyadmin.test", appsDir, appsDir);
            string pmaConf = BuildNginxVHostConfig(pmaProject, sslCertPath, sslKeyPath, phpFastCgiPort);
            File.WriteAllText(phpmyadminConfPath, pmaConf);
        }

        // Dá»n dáº¹p cÃ¡c file vhost tá»± Ä‘á»™ng cá»§a cÃ¡c dá»± Ã¡n Ä‘Ã£ bá»‹ xÃ³a
        var activeConfFiles = projects.Select(p => $"auto.{p.Domain}.conf").ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var file in Directory.GetFiles(NginxSitesEnabledDir, "auto.*.conf"))
        {
            string fileName = Path.GetFileName(file);
            if (!activeConfFiles.Contains(fileName))
            {
                try { File.Delete(file); } catch { }
            }
        }

        foreach (var p in projects)
        {
            domains.Add(p.Domain);
            string confPath = Path.Combine(NginxSitesEnabledDir, $"auto.{p.Domain}.conf");
            string confContent = BuildNginxVHostConfig(p, sslCertPath, sslKeyPath, phpFastCgiPort);
            File.WriteAllText(confPath, confContent);
        }

        // Cáº­p nháº­t file hosts
        HostsFileManager.SyncDomains(domains);
    }

    private static string BuildNginxVHostConfig(ProjectInfo project, string sslCert, string sslKey, int phpPort)
    {
        string normRoot = project.DocumentRoot.Replace('\\', '/');
        string normCert = sslCert.Replace('\\', '/');
        string normKey = sslKey.Replace('\\', '/');

        return $@"server {{
    listen 80;
    listen 443 ssl;
    server_name {project.Domain};

    root ""{normRoot}"";
    index index.php index.html index.htm;

    ssl_certificate ""{normCert}"";
    ssl_certificate_key ""{normKey}"";

    location / {{
        try_files $uri $uri/ /index.php?$query_string;
    }}

    location ~ \.php$ {{
        fastcgi_pass 127.0.0.1:{phpPort};
        fastcgi_index index.php;
        fastcgi_param SCRIPT_FILENAME $document_root$fastcgi_script_name;
        include fastcgi_params;
    }}
}}
";
    }
}

