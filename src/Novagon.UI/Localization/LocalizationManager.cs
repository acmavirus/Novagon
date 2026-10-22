using System;
using System.IO;
using System.Linq;
using System.Windows;
using Novagon.Core.Config;

namespace Novagon.UI.Localization;

public static class LocalizationManager
{
    public static string CurrentLanguage { get; private set; } = "vi";

    public static event Action? LanguageChanged;

    public static void Initialize()
    {
        var settings = AppSettings.Load();
        SetLanguage(settings.Language, saveSettings: false);
    }

    public static void SetLanguage(string langCode, bool saveSettings = true)
    {
        if (langCode != "vi" && langCode != "en")
            langCode = "vi";

        CurrentLanguage = langCode;

        string dictPath = $"Localization/Strings.{langCode}.xaml";
        var newDict = new ResourceDictionary
        {
            Source = new Uri(dictPath, UriKind.Relative)
        };

        var appResources = Application.Current.Resources;
        var existingDict = appResources.MergedDictionaries
            .FirstOrDefault(d => d.Source != null && d.Source.OriginalString.Contains("Localization/Strings."));

        if (existingDict != null)
        {
            int index = appResources.MergedDictionaries.IndexOf(existingDict);
            appResources.MergedDictionaries[index] = newDict;
        }
        else
        {
            appResources.MergedDictionaries.Add(newDict);
        }

        if (saveSettings)
        {
            var settings = AppSettings.Load();
            settings.Language = CurrentLanguage;
            settings.Save();
        }

        LanguageChanged?.Invoke();
    }

    public static string GetString(string key, string fallback = "")
    {
        if (Application.Current != null && Application.Current.TryFindResource(key) is string str)
        {
            return str;
        }
        return string.IsNullOrEmpty(fallback) ? key : fallback;
    }
}
