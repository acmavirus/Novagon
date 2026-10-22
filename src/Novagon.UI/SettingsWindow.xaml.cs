using System.Windows;
using System.Windows.Controls;
using Novagon.Core.Config;
using Novagon.UI.Localization;

namespace Novagon.UI;

public partial class SettingsWindow : Window
{
    private readonly AppSettings _settings;

    public SettingsWindow()
    {
        InitializeComponent();

        _settings = AppSettings.Load();

        // Nạp ngôn ngữ hiện tại vào dropdown ComboBox
        string currentLang = LocalizationManager.CurrentLanguage;
        if (currentLang == "en")
        {
            CmbLanguage.SelectedItem = OptLangEn;
        }
        else
        {
            CmbLanguage.SelectedItem = OptLangVi;
        }

        // TLD, AutoStart & StartWithWindows
        TxtTld.Text = _settings.DefaultTld;
        ChkAutoStart.IsChecked = _settings.AutoStartServices;
        ChkStartWithWindows.IsChecked = _settings.StartWithWindows;
    }

    private void BtnSave_Click(object sender, RoutedEventArgs e)
    {
        // 1. Áp dụng ngôn ngữ được chọn
        string selectedLang = "vi";
        if (CmbLanguage.SelectedItem is ComboBoxItem item && item.Tag is string tag)
        {
            selectedLang = tag;
        }

        _settings.Language = selectedLang;

        // 2. TLD & AutoStart
        string tld = TxtTld.Text.Trim().TrimStart('.');
        if (string.IsNullOrWhiteSpace(tld)) tld = "test";
        _settings.DefaultTld = tld;
        _settings.AutoStartServices = ChkAutoStart.IsChecked ?? false;

        // 3. Khởi động cùng Windows
        bool startWithWin = ChkStartWithWindows.IsChecked ?? false;
        _settings.StartWithWindows = startWithWin;
        ApplyStartWithWindows(startWithWin);

        _settings.Save();

        // 4. Chuyển đổi ngôn ngữ giao diện ngay lập tức
        LocalizationManager.SetLanguage(selectedLang, saveSettings: false);

        Close();
    }

    private static void ApplyStartWithWindows(bool enable)
    {
        try
        {
            const string runKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
            const string appName = "Novagon";
            using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(runKey, true);
            if (key != null)
            {
                if (enable)
                {
                    string? exePath = System.Environment.ProcessPath;
                    if (!string.IsNullOrEmpty(exePath))
                    {
                        key.SetValue(appName, $"\"{exePath}\"");
                    }
                }
                else
                {
                    key.DeleteValue(appName, false);
                }
            }
        }
        catch { }
    }

    private void BtnClose_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }
}
