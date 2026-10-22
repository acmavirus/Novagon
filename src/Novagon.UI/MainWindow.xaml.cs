using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Novagon.Core;
using Novagon.Core.Environment;
using Novagon.Core.Process;
using Novagon.Core.QuickApps;
using Novagon.Core.Runtimes;
using Novagon.Core.Services;
using Novagon.Core.VHost;

namespace Novagon.UI;

public partial class MainWindow : Window
{
    private readonly ServiceManager _serviceManager;
    private readonly PackageManager _packageManager;
    private readonly QuickAppManager _quickAppManager;
    private bool _isExiting;

    public MainWindow()
    {
        InitializeComponent();

        _serviceManager = new ServiceManager();
        _packageManager = new PackageManager();
        _quickAppManager = new QuickAppManager();
        _serviceManager.OnLogReceived += OnLogReceived;
        _serviceManager.OnStateChanged += OnStateChanged;

        if (!IsAdministrator())
        {
            BtnElevate.Visibility = Visibility.Visible;
        }

        LoadRuntimes();
        RefreshVHosts();
        RefreshPackageList();
        LoadExtensions();
        UpdateUIState();
    }

    private static bool IsAdministrator()
    {
        using var identity = System.Security.Principal.WindowsIdentity.GetCurrent();
        var principal = new System.Security.Principal.WindowsPrincipal(identity);
        return principal.IsInRole(System.Security.Principal.WindowsBuiltInRole.Administrator);
    }

    private void BtnElevate_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = System.Environment.ProcessPath ?? "Novagon.UI.exe",
                UseShellExecute = true,
                Verb = "runas"
            };
            Process.Start(psi);
            _isExiting = true;
            _serviceManager.Dispose();
            TrayIcon.Dispose();
            Close();
        }
        catch
        {
            // NgÆ°á»i dÃ¹ng chá»n No/Há»§y trÃªn há»™p thoáº¡i UAC
        }
    }

    private void LoadRuntimes()
    {
        var phps = _serviceManager.Scanner.GetInstalledPackages("php");
        CmbPhpVersions.ItemsSource = phps;
        CmbPhpVersions.DisplayMemberPath = "Name";
        if (phps.Count > 0)
        {
            CmbPhpVersions.SelectedItem = _serviceManager.ActivePhp ?? phps[0];
            TxtPhpTitle.Text = _serviceManager.ActivePhp?.Name ?? "PHP";
        }
        else
        {
            TxtPhpTitle.Text = "ChÆ°a cÃ i Ä‘áº·t";
        }

        var mysqls = _serviceManager.Scanner.GetInstalledPackages("mysql");
        CmbMySqlVersions.ItemsSource = mysqls;
        CmbMySqlVersions.DisplayMemberPath = "Name";
        if (mysqls.Count > 0)
        {
            CmbMySqlVersions.SelectedItem = _serviceManager.ActiveMySql ?? mysqls[0];
            TxtDbTitle.Text = _serviceManager.ActiveMySql?.Name ?? "MySQL";
        }
        else
        {
            TxtDbTitle.Text = "ChÆ°a cÃ i Ä‘áº·t";
        }

        var redises = _serviceManager.Scanner.GetInstalledPackages("redis");
        if (redises.Count > 0)
        {
            TxtRedisTitle.Text = redises[0].Name;
        }
        else
        {
            TxtRedisTitle.Text = "ChÆ°a cÃ i Ä‘áº·t (Hub)";
        }
    }

    private void RefreshVHosts()
    {
        var projects = _serviceManager.VHostGen.ScanProjects();
        ListVHosts.ItemsSource = projects;
    }

    private void OnStateChanged()
    {
        if (_isExiting) return;
        Dispatcher.BeginInvoke(UpdateUIState);
    }

    private void UpdateUIState()
    {
        if (_isExiting) return;
        bool isRunning = _serviceManager.IsRunning;

        BtnStartAll.IsEnabled = !isRunning;
        BtnStopAll.IsEnabled = isRunning;

        if (isRunning)
        {
            StatusDot.Fill = new SolidColorBrush(Color.FromRgb(34, 197, 94)); // Emerald / Green
            TxtStatus.Text = "Running (Online)";
        }
        else
        {
            StatusDot.Fill = new SolidColorBrush(Color.FromRgb(239, 68, 68)); // Red
            TxtStatus.Text = "Start All (Stopped)";
        }

        // Cáº­p nháº­t tráº¡ng thÃ¡i chi tiáº¿t tá»«ng Service
        bool hasNginx = _serviceManager.Scanner.GetInstalledPackages("nginx").Count > 0;
        bool hasDb = _serviceManager.ActiveMySql != null;
        bool hasPhp = _serviceManager.ActivePhp != null;
        bool hasRedis = _serviceManager.Scanner.GetInstalledPackages("redis").Count > 0;

        UpdateServiceBadge(BadgeWeb, TxtStatusWeb, _serviceManager.WebStatus, hasNginx);
        UpdateServiceBadge(BadgeDb, TxtStatusDb, _serviceManager.MySqlStatus, hasDb);
        UpdateServiceBadge(BadgePhp, TxtStatusPhp, _serviceManager.PhpStatus, hasPhp);
        UpdateServiceBadge(BadgeRedis, TxtStatusRedis, _serviceManager.RedisStatus, hasRedis);
    }

    private static void UpdateServiceBadge(Border badge, TextBlock txt, ProcessStatus status, bool isInstalled)
    {
        if (!isInstalled)
        {
            badge.Background = new SolidColorBrush(Color.FromRgb(241, 245, 249)); // #f1f5f9
            badge.BorderBrush = new SolidColorBrush(Color.FromRgb(203, 213, 225)); // #cbd5e1
            txt.Foreground = new SolidColorBrush(Color.FromRgb(148, 163, 184)); // #94a3b8
            txt.Text = "â—‹ ChÆ°a cÃ i";
            return;
        }

        switch (status)
        {
            case ProcessStatus.Running:
                badge.Background = new SolidColorBrush(Color.FromRgb(220, 252, 231)); // #dcfce7
                badge.BorderBrush = new SolidColorBrush(Color.FromRgb(134, 239, 172)); // #86efac
                txt.Foreground = new SolidColorBrush(Color.FromRgb(21, 128, 61)); // #15803d
                txt.Text = "â— Running";
                break;
            case ProcessStatus.Faulted:
                badge.Background = new SolidColorBrush(Color.FromRgb(254, 226, 226)); // #fee2e2
                badge.BorderBrush = new SolidColorBrush(Color.FromRgb(252, 165, 165)); // #fca5a5
                txt.Foreground = new SolidColorBrush(Color.FromRgb(185, 28, 28)); // #b91c1c
                txt.Text = "âš  Lá»—i (Error)";
                break;
            case ProcessStatus.Starting:
                badge.Background = new SolidColorBrush(Color.FromRgb(254, 249, 195)); // #fef9c3
                badge.BorderBrush = new SolidColorBrush(Color.FromRgb(253, 224, 71)); // #fde047
                txt.Foreground = new SolidColorBrush(Color.FromRgb(161, 98, 7)); // #a16207
                txt.Text = "â³ Starting...";
                break;
            default:
                badge.Background = new SolidColorBrush(Color.FromRgb(241, 245, 249)); // #f1f5f9
                badge.BorderBrush = new SolidColorBrush(Color.FromRgb(203, 213, 225)); // #cbd5e1
                txt.Foreground = new SolidColorBrush(Color.FromRgb(100, 116, 139)); // #64748b
                txt.Text = "â—‹ Stopped";
                break;
        }
    }

    private void OnLogReceived(string source, string message)
    {
        if (_isExiting) return;
        Dispatcher.BeginInvoke(() =>
        {
            if (_isExiting) return;
            TxtLogs.AppendText(message + System.Environment.NewLine);
            TxtLogs.ScrollToEnd();
        });
    }

    private async void BtnStartAll_Click(object sender, RoutedEventArgs e)
    {
        BtnStartAll.IsEnabled = false;
        LoadRuntimes();
        await Task.Run(() => _serviceManager.StartAll());
        RefreshVHosts();
    }

    private async void BtnStopAll_Click(object sender, RoutedEventArgs e)
    {
        BtnStopAll.IsEnabled = false;
        await Task.Run(() => _serviceManager.StopAll());
    }

    private async void BtnReload_Click(object sender, RoutedEventArgs e)
    {
        BtnReload.IsEnabled = false;
        LoadRuntimes();
        await Task.Run(() => _serviceManager.ReloadVHosts());
        RefreshVHosts();
        BtnReload.IsEnabled = true;
    }

    private void BtnTerminal_Click(object sender, RoutedEventArgs e)
    {
        EnvironmentManager.OpenTerminal(
            _serviceManager.ActivePhp?.RootPath,
            _serviceManager.ActiveMySql?.RootPath,
            _serviceManager.VHostGen.WwwDirectory
        );
    }

    private void BtnDatabase_Click(object sender, RoutedEventArgs e)
    {
        string url = "http://127.0.0.1/phpmyadmin";
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = url,
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            MessageBox.Show($"KhÃ´ng thá»ƒ má»Ÿ trÃ¬nh duyá»‡t: {ex.Message}", "Lá»—i", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private async void AfterProjectCreated(string projectName, string projectDir, string successMessage)
    {
        RefreshVHosts();
        _serviceManager.ReloadVHosts();

        TxtQuickAppStatus.Text = $"âœ“ {successMessage} (Domain: {projectName}.test)";
        PnlQuickAppProgress.Visibility = Visibility.Visible;
        PbQuickApp.Value = 100;

        // Äá»£i 800ms Ä‘á»ƒ Nginx master hoÃ n táº¥t reload cÃ¡c worker, Ä‘áº£m báº£o khi má»Ÿ trÃ¬nh duyá»‡t vhost má»›i Ä‘Ã£ sáºµn sÃ ng
        await Task.Delay(800);

        // Má»Ÿ trÃ¬nh duyá»‡t náº¿u Ä‘Æ°á»£c chá»n
        if (ChkOpenBrowser?.IsChecked == true)
        {
            string url = $"http://{projectName}.test";
            try
            {
                Process.Start(new ProcessStartInfo { FileName = url, UseShellExecute = true });
            }
            catch { }
        }

        // Má»Ÿ thÆ° má»¥c dá»± Ã¡n trong Explorer náº¿u Ä‘Æ°á»£c chá»n
        if (ChkOpenFolder?.IsChecked == true && Directory.Exists(projectDir))
        {
            try
            {
                Process.Start("explorer.exe", projectDir);
            }
            catch { }
        }
    }

    private async Task EnsureServicesRunningAsync()
    {
        if (!_serviceManager.IsRunning || _serviceManager.MySqlStatus != ProcessStatus.Running)
        {
            PnlQuickAppProgress.Visibility = Visibility.Visible;
            TxtQuickAppStatus.Text = "Äang kÃ­ch hoáº¡t mÃ¡y chá»§ Nginx, PHP vÃ  MariaDB MySQL...";
            await Task.Run(() => _serviceManager.StartAll());
            // Äá»£i MySQL á»•n Ä‘á»‹nh nháº­n socket
            await Task.Delay(1000);
        }
    }

    private async void BtnCreateBlankPhp_Click(object sender, RoutedEventArgs e)
    {
        string name = TxtQuickAppName.Text.Trim();
        if (string.IsNullOrWhiteSpace(name))
        {
            MessageBox.Show("Vui lÃ²ng nháº­p tÃªn dá»± Ã¡n!", "ThÃ´ng bÃ¡o", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        bool createDb = ChkCreateDb.IsChecked ?? true;
        if (createDb)
        {
            await EnsureServicesRunningAsync();
        }

        if (_quickAppManager.CreateBlankPhpApp(name, createDb, out string projectDir))
        {
            AfterProjectCreated(name, projectDir, $"ÄÃ£ táº¡o dá»± Ã¡n Blank PHP '{name}'");
        }
        else
        {
            MessageBox.Show($"ThÆ° má»¥c dá»± Ã¡n '{name}' Ä‘Ã£ tá»“n táº¡i trong www/", "ThÃ´ng bÃ¡o", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private async void BtnCreateLaravel_Click(object sender, RoutedEventArgs e)
    {
        string name = TxtQuickAppName.Text.Trim();
        if (string.IsNullOrWhiteSpace(name))
        {
            MessageBox.Show("Vui lÃ²ng nháº­p tÃªn dá»± Ã¡n!", "ThÃ´ng bÃ¡o", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var btn = sender as Button;
        if (btn != null) btn.IsEnabled = false;

        try
        {
            bool createDb = ChkCreateDb.IsChecked ?? true;
            if (createDb)
            {
                await EnsureServicesRunningAsync();
            }

            PnlQuickAppProgress.Visibility = Visibility.Visible;
            PbQuickApp.Value = 0;
            TxtQuickAppStatus.Text = "Äang khá»Ÿi táº¡o dá»± Ã¡n Laravel qua Composer...";

            var result = await Task.Run(() => _quickAppManager.CreateLaravelAppAsync(name, createDb, (pct, msg) =>
            {
                Dispatcher.BeginInvoke(() =>
                {
                    PbQuickApp.Value = pct;
                    TxtQuickAppStatus.Text = msg;
                });
            }));

            if (result.Success)
            {
                AfterProjectCreated(name, result.ProjectDir, result.Message);
            }
            else
            {
                TxtQuickAppStatus.Text = "âœ— " + result.Message;
                MessageBox.Show(result.Message, "Lá»—i táº¡o Laravel", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }
        catch (Exception ex)
        {
            TxtQuickAppStatus.Text = "âœ— " + ex.Message;
            MessageBox.Show($"Lá»—i trong quÃ¡ trÃ¬nh táº¡o dá»± Ã¡n: {ex.Message}", "Lá»—i ngoáº¡i lá»‡", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            if (btn != null) btn.IsEnabled = true;
        }
    }

    private async void BtnCreateStaticWeb_Click(object sender, RoutedEventArgs e)
    {
        string name = TxtQuickAppName.Text.Trim();
        if (string.IsNullOrWhiteSpace(name))
        {
            MessageBox.Show("Vui lÃ²ng nháº­p tÃªn dá»± Ã¡n!", "ThÃ´ng bÃ¡o", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        if (!_serviceManager.IsRunning)
        {
            await EnsureServicesRunningAsync();
        }

        if (_quickAppManager.CreateStaticWebApp(name, out string projectDir))
        {
            AfterProjectCreated(name, projectDir, $"ÄÃ£ táº¡o dá»± Ã¡n Static Web '{name}'");
        }
        else
        {
            MessageBox.Show($"ThÆ° má»¥c dá»± Ã¡n '{name}' Ä‘Ã£ tá»“n táº¡i trong www/", "ThÃ´ng bÃ¡o", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private async void BtnCreateWordPress_Click(object sender, RoutedEventArgs e)
    {
        string name = TxtQuickAppName.Text.Trim();
        if (string.IsNullOrWhiteSpace(name))
        {
            MessageBox.Show("Vui lÃ²ng nháº­p tÃªn dá»± Ã¡n!", "ThÃ´ng bÃ¡o", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var btn = sender as Button;
        if (btn != null) btn.IsEnabled = false;

        try
        {
            bool createDb = ChkCreateDb.IsChecked ?? true;
            await EnsureServicesRunningAsync();

            PnlQuickAppProgress.Visibility = Visibility.Visible;
            PbQuickApp.Value = 0;
            TxtQuickAppStatus.Text = "Äang chuáº©n bá»‹ gÃ³i WordPress tá»« wordpress.org...";

            var result = await Task.Run(() => _quickAppManager.CreateWordPressAppAsync(name, createDb, (pct, msg) =>
            {
                Dispatcher.BeginInvoke(() =>
                {
                    PbQuickApp.Value = pct;
                    TxtQuickAppStatus.Text = msg;
                });
            }));

            if (result.Success)
            {
                AfterProjectCreated(name, result.ProjectDir, result.Message);
            }
            else
            {
                TxtQuickAppStatus.Text = "âœ— " + result.Message;
                MessageBox.Show(result.Message, "Lá»—i táº¡o WordPress", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }
        catch (Exception ex)
        {
            TxtQuickAppStatus.Text = "âœ— " + ex.Message;
            MessageBox.Show($"Lá»—i trong quÃ¡ trÃ¬nh táº¡o dá»± Ã¡n: {ex.Message}", "Lá»—i ngoáº¡i lá»‡", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            if (btn != null) btn.IsEnabled = true;
        }
    }

    private void LoadExtensions()
    {
        if (_serviceManager.ActivePhp != null)
        {
            var exts = PhpExtensionManager.GetExtensions(_serviceManager.ActivePhp.RootPath);
            ListPhpExtensions.ItemsSource = exts;
        }
    }

    private void ChkExtension_Click(object sender, RoutedEventArgs e)
    {
        if (sender is CheckBox chk && chk.Tag is string extName && _serviceManager.ActivePhp != null)
        {
            bool isChecked = chk.IsChecked ?? false;
            PhpExtensionManager.ToggleExtension(_serviceManager.ActivePhp.RootPath, extName, isChecked);
            if (_serviceManager.IsRunning)
            {
                _serviceManager.ReloadVHosts();
            }
        }
    }

    private void BtnOpenWww_Click(object sender, RoutedEventArgs e)
    {
        string www = _serviceManager.VHostGen.WwwDirectory;
        Directory.CreateDirectory(www);
        Process.Start("explorer.exe", www);
    }

    private void BtnOpenDomain_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is string domain)
        {
            string url = $"https://{domain}";
            Process.Start(new ProcessStartInfo
            {
                FileName = url,
                UseShellExecute = true
            });
        }
    }

    private async void BtnDeleteProject_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is ProjectInfo project)
        {
            var confirm = MessageBox.Show(
                $"Báº¡n cÃ³ cháº¯c cháº¯n muá»‘n xÃ³a dá»± Ã¡n '{project.Name}'?\n\nDomain: {project.Domain}\nThÆ° má»¥c: {project.ProjectPath}\n\nThao tÃ¡c nÃ y sáº½ xÃ³a vÄ©nh viá»…n thÆ° má»¥c dá»± Ã¡n vÃ  cáº¥u hÃ¬nh Virtual Host tÆ°Æ¡ng á»©ng!",
                "XÃ¡c nháº­n xÃ³a dá»± Ã¡n",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (confirm == MessageBoxResult.Yes)
            {
                try
                {
                    // 1. XÃ³a file cáº¥u hÃ¬nh Nginx VHost náº¿u cÃ³
                    string confPath = Path.Combine(_serviceManager.VHostGen.NginxSitesEnabledDir, $"auto.{project.Domain}.conf");
                    if (File.Exists(confPath))
                    {
                        try { File.Delete(confPath); } catch { }
                    }

                    // 2. XÃ³a thÆ° má»¥c dá»± Ã¡n trong www/
                    if (Directory.Exists(project.ProjectPath))
                    {
                        Directory.Delete(project.ProjectPath, recursive: true);
                    }

                    // 3. Táº£i láº¡i danh sÃ¡ch vÃ  Ä‘á»“ng bá»™ VHosts
                    RefreshVHosts();
                    await Task.Run(() => _serviceManager.ReloadVHosts());

                    MessageBox.Show($"ÄÃ£ xÃ³a dá»± Ã¡n '{project.Name}' thÃ nh cÃ´ng!", "ThÃ´ng bÃ¡o", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"KhÃ´ng thá»ƒ xÃ³a dá»± Ã¡n: {ex.Message}", "Lá»—i", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }
    }

    private void CmbPhpVersions_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (CmbPhpVersions.SelectedItem is RuntimePackage pkg)
        {
            _serviceManager.ActivePhp = pkg;
            TxtPhpTitle.Text = pkg.Name;
            LoadExtensions();
        }
    }

    private void CmbMySqlVersions_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (CmbMySqlVersions.SelectedItem is RuntimePackage pkg)
        {
            _serviceManager.ActiveMySql = pkg;
            TxtDbTitle.Text = pkg.Name;
        }
    }

    private void BtnClearLogs_Click(object sender, RoutedEventArgs e)
    {
        TxtLogs.Clear();
    }

    private void TrayIcon_TrayLeftMouseDown(object sender, RoutedEventArgs e)
    {
        ToggleWindow();
    }

    private void MenuShowDashboard_Click(object sender, RoutedEventArgs e)
    {
        ShowWindow();
    }

    private void ToggleWindow()
    {
        if (Visibility == Visibility.Visible && WindowState != WindowState.Minimized)
        {
            Hide();
        }
        else
        {
            ShowWindow();
        }
    }

    private void ShowWindow()
    {
        Show();
        WindowState = WindowState.Normal;
        Activate();
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        if (!_isExiting)
        {
            // Thay vÃ¬ táº¯t háº³n, áº©n vÃ o Khay há»‡ thá»‘ng
            e.Cancel = true;
            Hide();
        }
        else
        {
            base.OnClosing(e);
        }
    }

    private async void MenuExit_Click(object sender, RoutedEventArgs e)
    {
        if (_isExiting) return;
        _isExiting = true;

        // 1. áº¨n ngay TrayIcon á»Ÿ khay há»‡ thá»‘ng Ä‘á»ƒ táº¡o pháº£n há»“i tá»©c thÃ¬
        try
        {
            TrayIcon.Visibility = Visibility.Collapsed;
            TrayIcon.Dispose();
        }
        catch { }

        // 2. áº¨n cá»­a sá»• ngay
        try
        {
            Hide();
        }
        catch { }

        // 3. Dá»«ng dá»‹ch vá»¥ trÃªn background task vá»›i timeout 1.5 giÃ¢y
        try
        {
            await Task.Run(() =>
            {
                _serviceManager.StopAll();
                _serviceManager.Dispose();
            }).WaitAsync(TimeSpan.FromMilliseconds(1500));
        }
        catch { }

        // 4. QuÃ©t sáº¡ch cÃ¡c tiáº¿n trÃ¬nh con Novagon cÃ²n sÃ³t láº¡i
        KillOrphanedNovagonProcesses();

        System.Windows.Application.Current.Shutdown();
        System.Environment.Exit(0);
    }

    private void KillOrphanedNovagonProcesses()
    {
        try
        {
            string root = AppDirectoryHelper.AppRoot.ToLowerInvariant();
            var targetNames = new[] { "nginx", "php-cgi", "mysqld" };
            foreach (var name in targetNames)
            {
                var procs = Process.GetProcessesByName(name);
                foreach (var p in procs)
                {
                    try
                    {
                        string path = p.MainModule?.FileName?.ToLowerInvariant() ?? "";
                        if (path.StartsWith(root))
                        {
                            p.Kill();
                        }
                    }
                    catch { }
                    finally
                    {
                        try { p.Dispose(); } catch { }
                    }
                }
            }
        }
        catch { }
    }

    private void RefreshPackageList()
    {
        var packages = _packageManager.GetAvailablePackages();
        ListPackages.ItemsSource = packages;
    }

    private async void BtnDownloadPackage_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is AvailablePackage pkg)
        {
            btn.IsEnabled = false;
            PnlDownloadProgress.Visibility = Visibility.Visible;
            PbDownload.Value = 0;
            TxtDownloadStatus.Text = $"Báº¯t Ä‘áº§u táº£i {pkg.DisplayName}...";

            var result = await Task.Run(() => _packageManager.DownloadAndInstallAsync(pkg, (pct, msg) =>
            {
                Dispatcher.BeginInvoke(() =>
                {
                    PbDownload.Value = pct;
                    TxtDownloadStatus.Text = msg;
                });
            }));

            if (result.Success)
            {
                TxtDownloadStatus.Text = $"âœ“ {result.Message}";
                LoadRuntimes();
                RefreshPackageList();
            }
            else
            {
                TxtDownloadStatus.Text = $"âœ— {result.Message}";
                btn.IsEnabled = true;
            }
        }
    }
}
