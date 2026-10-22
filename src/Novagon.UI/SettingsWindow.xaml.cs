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

        // TLD & AutoStart
        TxtTld.Text = _settings.DefaultTld;
        ChkAutoStart.IsChecked = _settings.AutoStartServices;
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

        _settings.Save();

        // 3. Chuyển đổi ngôn ngữ giao diện ngay lập tức
        LocalizationManager.SetLanguage(selectedLang, saveSettings: false);

        Close();
    }

    private void BtnClose_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }
}
