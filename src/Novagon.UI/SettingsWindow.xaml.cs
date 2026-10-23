using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using Novagon.Core;
using Novagon.Core.Config;
using Novagon.UI.Localization;

namespace Novagon.UI;

public partial class SettingsWindow : Window
{
    private readonly AppSettings _settings;
    public bool SettingsSaved { get; private set; }

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

        // Root Directory
        TxtRootDir.Text = _settings.GetEffectiveRootDirectory();

        // TLD, AutoStart & StartWithWindows
        TxtTld.Text = _settings.DefaultTld;
        ChkAutoStart.IsChecked = _settings.AutoStartServices;
        ChkStartWithWindows.IsChecked = _settings.StartWithWindows;
    }

    private void BtnBrowseRootDir_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var dialog = new Microsoft.Win32.OpenFolderDialog
            {
                Title = LocalizationManager.GetString("Dialog_SelectRootDirTitle", "Chọn thư mục Root cho dự án từ ổ đĩa"),
                InitialDirectory = Directory.Exists(TxtRootDir.Text) ? TxtRootDir.Text : AppDirectoryHelper.AppRoot,
                Multiselect = false
            };

            if (dialog.ShowDialog(this) == true && !string.IsNullOrWhiteSpace(dialog.FolderName))
            {
                TxtRootDir.Text = dialog.FolderName;
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, LocalizationManager.GetString("Msg_ErrorTitle", "Lỗi"), MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void BtnDefaultRootDir_Click(object sender, RoutedEventArgs e)
    {
        TxtRootDir.Text = System.IO.Path.Combine(Novagon.Core.AppDirectoryHelper.AppRoot, "www");
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

        // 2. Thư mục Root dự án
        string enteredRoot = TxtRootDir.Text.Trim();
        string defaultWww = System.IO.Path.Combine(Novagon.Core.AppDirectoryHelper.AppRoot, "www");
        if (string.IsNullOrWhiteSpace(enteredRoot) || string.Equals(System.IO.Path.GetFullPath(enteredRoot), System.IO.Path.GetFullPath(defaultWww), StringComparison.OrdinalIgnoreCase))
        {
            _settings.RootDirectory = null;
        }
        else
        {
            try
            {
                string fullPath = System.IO.Path.GetFullPath(enteredRoot);
                _settings.RootDirectory = fullPath;
                System.IO.Directory.CreateDirectory(fullPath);
            }
            catch
            {
                _settings.RootDirectory = enteredRoot;
            }
        }

        // 3. TLD & AutoStart
        string tld = TxtTld.Text.Trim().TrimStart('.');
        if (string.IsNullOrWhiteSpace(tld)) tld = "test";
        _settings.DefaultTld = tld;
        _settings.AutoStartServices = ChkAutoStart.IsChecked ?? false;

        // 4. Khởi động cùng Windows
        bool startWithWin = ChkStartWithWindows.IsChecked ?? false;
        _settings.StartWithWindows = startWithWin;
        ApplyStartWithWindows(startWithWin);

        _settings.Save();

        // 5. Chuyển đổi ngôn ngữ giao diện ngay lập tức
        LocalizationManager.SetLanguage(selectedLang, saveSettings: false);

        SettingsSaved = true;
        DialogResult = true;
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
