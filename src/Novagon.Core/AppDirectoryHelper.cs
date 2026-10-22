using System;
using System.IO;

namespace Novagon.Core;

/// <summary>
/// XÃ¡c Ä‘á»‹nh thÆ° má»¥c gá»‘c cá»§a Novagon má»™t cÃ¡ch thÃ´ng minh,
/// hoáº¡t Ä‘á»™ng chÃ­nh xÃ¡c cáº£ khi cháº¡y Debug (trong src/.../bin/Debug) láº«n khi cháº¡y Portable thá»±c táº¿.
/// </summary>
public static class AppDirectoryHelper
{
    private static string? _appRoot;

    public static string AppRoot
    {
        get
        {
            if (_appRoot != null) return _appRoot;

            string current = AppContext.BaseDirectory;
            while (!string.IsNullOrEmpty(current))
            {
                if (Directory.Exists(Path.Combine(current, "bin")) &&
                    (Directory.Exists(Path.Combine(current, "www")) || Directory.Exists(Path.Combine(current, "src"))))
                {
                    _appRoot = current;
                    return _appRoot;
                }
                var parent = Directory.GetParent(current);
                if (parent == null || parent.FullName == current)
                    break;
                current = parent.FullName;
            }

            _appRoot = AppContext.BaseDirectory;
            return _appRoot;
        }
        set => _appRoot = value;
    }
}

