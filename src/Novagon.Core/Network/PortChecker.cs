using System;
using System.Linq;
using System.Net.NetworkInformation;

namespace Novagon.Core.Network;

/// <summary>
/// Kiá»ƒm tra tráº¡ng thÃ¡i cá»•ng máº¡ng TCP Ä‘á»ƒ cáº£nh bÃ¡o sá»›m tÃ¬nh tráº¡ng trÃ¹ng cá»•ng trÆ°á»›c khi khá»Ÿi cháº¡y dá»‹ch vá»¥.
/// </summary>
public static class PortChecker
{
    /// <summary>
    /// Kiá»ƒm tra xem cá»•ng TCP cÃ³ Ä‘ang bá»‹ á»©ng dá»¥ng khÃ¡c chiáº¿m giá»¯ hay khÃ´ng.
    /// </summary>
    public static bool IsPortInUse(int port)
    {
        try
        {
            var ipGlobalProperties = IPGlobalProperties.GetIPGlobalProperties();
            var tcpListeners = ipGlobalProperties.GetActiveTcpListeners();
            return tcpListeners.Any(endpoint => endpoint.Port == port);
        }
        catch
        {
            return false;
        }
    }
}

