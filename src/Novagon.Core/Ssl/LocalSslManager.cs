using System;
using System.IO;
using System.Net;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace Novagon.Core.Ssl;

/// <summary>
/// Quáº£n lÃ½ chá»©ng chá»‰ SSL cá»¥c bá»™ tá»± kÃ½ chuáº©n xÃ¡c theo tiÃªu chuáº©n X.509.
/// Tá»± Ä‘á»™ng táº¡o Root CA, cÃ i Ä‘áº·t vÃ o Windows Trusted Root Certification Authorities
/// vÃ  cáº¥p chá»©ng chá»‰ Wildcard (*.test) cho Web Server (Nginx/Apache).
/// </summary>
public static class LocalSslManager
{
    private const string CaCommonName = "Novagon Local Root CA";
    private static string SslDir => Path.Combine(AppDirectoryHelper.AppRoot, "etc", "ssl");

    public static string CertPath => Path.Combine(SslDir, "novagon.crt");
    public static string KeyPath => Path.Combine(SslDir, "novagon.key");

    public static bool EnsureCertificatesInstalled(bool installToWindowsTrustStore = true)
    {
        try
        {
            Directory.CreateDirectory(SslDir);

            // Kiá»ƒm tra xem certificate Ä‘Ã£ tá»“n táº¡i trÃªn Ä‘Ä©a chÆ°a
            if (File.Exists(CertPath) && File.Exists(KeyPath))
            {
                return true;
            }

            // 1. Táº¡o Root CA
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

            // CÃ i Root CA vÃ o Windows Trust Store náº¿u Ä‘Æ°á»£c yÃªu cáº§u
            if (installToWindowsTrustStore)
            {
                InstallRootCaToWindows(rootCert);
            }

            // 2. Táº¡o chá»©ng chá»‰ Wildcard cho *.test, localhost, 127.0.0.1
            using var leafKey = RSA.Create(2048);
            var leafReq = new CertificateRequest(
                "CN=*.test",
                leafKey,
                HashAlgorithmName.SHA256,
                RSASignaturePadding.Pkcs1);

            var sanBuilder = new SubjectAlternativeNameBuilder();
            sanBuilder.AddDnsName("*.test");
            sanBuilder.AddDnsName("localhost");
            sanBuilder.AddIpAddress(IPAddress.Loopback);

            leafReq.CertificateExtensions.Add(sanBuilder.Build());
            leafReq.CertificateExtensions.Add(new X509EnhancedKeyUsageExtension(
                new OidCollection { new Oid("1.3.6.1.5.5.7.3.1") }, // Server Authentication
                critical: false));

            var serial = new byte[8];
            RandomNumberGenerator.Fill(serial);

            using var leafSigned = leafReq.Create(rootCert, now.AddDays(-1), now.AddYears(5), serial);
            using var leafWithKey = leafSigned.CopyWithPrivateKey(leafKey);

            // Xuáº¥t ra file PEM (.crt vÃ  .key) cho Nginx vÃ  Apache
            File.WriteAllText(CertPath, leafWithKey.ExportCertificatePem());
            File.WriteAllText(KeyPath, leafKey.ExportPkcs8PrivateKeyPem());

            return true;
        }
        catch
        {
            return false;
        }
    }

    private static void InstallRootCaToWindows(X509Certificate2 rootCert)
    {
        try
        {
            using var store = new X509Store(StoreName.Root, StoreLocation.LocalMachine);
            store.Open(OpenFlags.ReadWrite);

            // Kiá»ƒm tra xem Ä‘Ã£ cÃ³ CA cÅ© chÆ°a
            var existing = store.Certificates.Find(X509FindType.FindBySubjectName, CaCommonName, false);
            if (existing.Count == 0)
            {
                store.Add(rootCert);
            }
        }
        catch
        {
            // Náº¿u khÃ´ng ghi Ä‘Æ°á»£c vÃ o LocalMachine (cáº§n quyá»n admin), thá»­ ghi vÃ o CurrentUser
            try
            {
                using var userStore = new X509Store(StoreName.Root, StoreLocation.CurrentUser);
                userStore.Open(OpenFlags.ReadWrite);
                userStore.Add(rootCert);
            }
            catch { }
        }
    }
}

