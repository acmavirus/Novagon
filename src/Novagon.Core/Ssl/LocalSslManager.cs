using System;
using System.IO;
using System.Net;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace Novagon.Core.Ssl;

/// <summary>
/// Quản lý chứng chỉ SSL cục bộ tự ký chuẩn xác theo tiêu chuẩn X.509.
/// Tự động tạo Root CA, cài đặt vào Windows Trusted Root Certification Authorities
/// và cấp chứng chỉ Wildcard (*.test) cho Web Server (Nginx/Apache).
/// </summary>
public static class LocalSslManager
{
    private const string CaCommonName = "Novagon Local Root CA";
    private static string SslDir => Path.Combine(AppDirectoryHelper.AppRoot, "etc", "ssl");

    public static string CertPath => Path.Combine(SslDir, "novagon.crt");
    public static string KeyPath => Path.Combine(SslDir, "novagon.key");
    public static string CaCertPath => Path.Combine(SslDir, "ca.crt");
    public static string CaKeyPath => Path.Combine(SslDir, "ca.key");

    public static bool EnsureCertificatesInstalled(bool installToWindowsTrustStore = true)
    {
        try
        {
            Directory.CreateDirectory(SslDir);

            // Kiểm tra xem certificate đã tồn tại trên đĩa và hợp lệ chưa
            if (File.Exists(CertPath) && File.Exists(KeyPath) && File.Exists(CaCertPath) && IsValidCertificate(CertPath))
            {
                if (installToWindowsTrustStore && !IsCaInstalledInWindows())
                {
                    InstallRootCaToWindows(null, CaCertPath);
                }
                return true;
            }

            // 1. Tạo Root CA
            using var rootKey = RSA.Create(2048);
            var caReq = new CertificateRequest(
                $"CN={CaCommonName}, O=Novagon Development, C=VN",
                rootKey,
                HashAlgorithmName.SHA256,
                RSASignaturePadding.Pkcs1);

            caReq.CertificateExtensions.Add(new X509BasicConstraintsExtension(certificateAuthority: true, hasPathLengthConstraint: true, pathLengthConstraint: 0, critical: true));
            caReq.CertificateExtensions.Add(new X509KeyUsageExtension(X509KeyUsageFlags.KeyCertSign | X509KeyUsageFlags.CrlSign, critical: true));

            var now = DateTimeOffset.UtcNow;
            using var rootCert = caReq.CreateSelfSigned(now.AddDays(-1), now.AddYears(10));

            // Xuất Root CA ra đĩa (ca.crt, ca.key)
            string caPem = rootCert.ExportCertificatePem();
            File.WriteAllText(CaCertPath, caPem);
            File.WriteAllText(CaKeyPath, rootKey.ExportPkcs8PrivateKeyPem());

            // Cài Root CA vào Windows Trust Store nếu được yêu cầu
            if (installToWindowsTrustStore)
            {
                InstallRootCaToWindows(rootCert, CaCertPath);
            }

            // 2. Tạo chứng chỉ Wildcard cho *.test, localhost, 127.0.0.1
            using var leafKey = RSA.Create(2048);
            var leafReq = new CertificateRequest(
                "CN=*.test",
                leafKey,
                HashAlgorithmName.SHA256,
                RSASignaturePadding.Pkcs1);

            var sanBuilder = new SubjectAlternativeNameBuilder();
            sanBuilder.AddDnsName("*.test");
            sanBuilder.AddDnsName("localhost");
            sanBuilder.AddDnsName("*.local");
            sanBuilder.AddIpAddress(IPAddress.Loopback);

            leafReq.CertificateExtensions.Add(sanBuilder.Build());
            leafReq.CertificateExtensions.Add(new X509EnhancedKeyUsageExtension(
                new OidCollection { new Oid("1.3.6.1.5.5.7.3.1") }, // Server Authentication
                critical: false));

            var serial = new byte[8];
            RandomNumberGenerator.Fill(serial);

            using var leafSigned = leafReq.Create(rootCert, now.AddDays(-1), now.AddYears(5), serial);
            using var leafWithKey = leafSigned.CopyWithPrivateKey(leafKey);

            // Xuất fullchain PEM (.crt và .key) cho Nginx và Apache
            string fullChainPem = leafWithKey.ExportCertificatePem() + System.Environment.NewLine + caPem;
            string leafKeyPem = leafKey.ExportPkcs8PrivateKeyPem();

            File.WriteAllText(CertPath, fullChainPem);
            File.WriteAllText(KeyPath, leafKeyPem);

            // Đồng bộ sang laragon.crt/laragon.key nếu có cấu hình cũ
            try
            {
                File.WriteAllText(Path.Combine(SslDir, "laragon.crt"), fullChainPem);
                File.WriteAllText(Path.Combine(SslDir, "laragon.key"), leafKeyPem);
            }
            catch { }

            return true;
        }
        catch
        {
            return false;
        }
    }

    private static bool IsValidCertificate(string certFile)
    {
        try
        {
            using var cert = X509CertificateLoader.LoadCertificateFromFile(certFile);
            var now = DateTime.Now;
            if (now < cert.NotBefore || now > cert.NotAfter) return false;
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static bool IsCaInstalledInWindows()
    {
        try
        {
            using var userStore = new X509Store(StoreName.Root, StoreLocation.CurrentUser);
            userStore.Open(OpenFlags.ReadOnly);
            var existingUser = userStore.Certificates.Find(X509FindType.FindBySubjectName, CaCommonName, false);
            if (existingUser.Count > 0) return true;

            using var lmStore = new X509Store(StoreName.Root, StoreLocation.LocalMachine);
            lmStore.Open(OpenFlags.ReadOnly);
            var existingLm = lmStore.Certificates.Find(X509FindType.FindBySubjectName, CaCommonName, false);
            if (existingLm.Count > 0) return true;
        }
        catch { }

        return false;
    }

    private static void InstallRootCaToWindows(X509Certificate2? rootCert, string caCertPath)
    {
        if (rootCert == null && File.Exists(caCertPath))
        {
            try { rootCert = X509CertificateLoader.LoadCertificateFromFile(caCertPath); } catch { }
        }

        if (rootCert != null)
        {
            try
            {
                using var userStore = new X509Store(StoreName.Root, StoreLocation.CurrentUser);
                userStore.Open(OpenFlags.ReadWrite);
                userStore.Add(rootCert);
            }
            catch { }

            try
            {
                using var lmStore = new X509Store(StoreName.Root, StoreLocation.LocalMachine);
                lmStore.Open(OpenFlags.ReadWrite);
                lmStore.Add(rootCert);
            }
            catch { }
        }

        // Chạy certutil để đăng ký tin cậy vào Windows Root Certificate Authority
        try
        {
            if (File.Exists(caCertPath))
            {
                var psi = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "certutil.exe",
                    Arguments = $"-addstore -f \"Root\" \"{caCertPath}\"",
                    CreateNoWindow = true,
                    UseShellExecute = false
                };
                using var p = System.Diagnostics.Process.Start(psi);
                p?.WaitForExit(3000);
            }
        }
        catch { }
    }
}
