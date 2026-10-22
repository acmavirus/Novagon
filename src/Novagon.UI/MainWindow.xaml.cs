using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
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
using Novagon.UI.Localization;

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

        LocalizationManager.LanguageChanged += OnLanguageChanged;

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
            // Người dùng chọn No/Hủy trên hộp thoại UAC
        }
    }

    private void BtnSettings_Click(object sender, RoutedEventArgs e)
    {
        OpenSettingsDialog();
    }

    private void MenuSettings_Click(object sender, RoutedEventArgs e)
    {
        ShowWindow();
        OpenSettingsDialog();
    }

    private void OpenSettingsDialog()
    {
        var dlg = new SettingsWindow { Owner = this };
        dlg.ShowDialog();
    }

    private void OnLanguageChanged()
    {
        if (_isExiting) return;
        Dispatcher.BeginInvoke(() =>
        {
            UpdateUIState();
            LoadRuntimes();
            LoadExtensions();
            RefreshPackageList();
        });
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
            TxtPhpTitle.Text = LocalizationManager.GetString("Status_NotInstalled", "Chưa cài đặt");
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
            TxtDbTitle.Text = LocalizationManager.GetString("Status_NotInstalled", "Chưa cài đặt");
        }

        var redises = _serviceManager.Scanner.GetInstalledPackages("redis");
        if (redises.Count > 0)
        {
            TxtRedisTitle.Text = redises[0].Name;
        }
        else
        {
            TxtRedisTitle.Text = LocalizationManager.GetString("Status_NotInstalled_Hub", "Chưa cài đặt (Hub)");
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
            TxtStatus.Text = LocalizationManager.GetString("Overall_Online", "Running (Online)");
        }
        else
        {
            StatusDot.Fill = new SolidColorBrush(Color.FromRgb(239, 68, 68)); // Red
            TxtStatus.Text = LocalizationManager.GetString("Overall_Stopped", "Start All (Stopped)");
        }

        // Cập nhật trạng thái chi tiết từng Service
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
            txt.Text = LocalizationManager.GetString("Status_NotInstalled_Short", "○ Chưa cài");
            return;
        }

        switch (status)
        {
            case ProcessStatus.Running:
                badge.Background = new SolidColorBrush(Color.FromRgb(220, 252, 231)); // #dcfce7
                badge.BorderBrush = new SolidColorBrush(Color.FromRgb(134, 239, 172)); // #86efac
                txt.Foreground = new SolidColorBrush(Color.FromRgb(21, 128, 61)); // #15803d
                txt.Text = LocalizationManager.GetString("Status_Running_Badge", "● Running");
                break;
            case ProcessStatus.Faulted:
                badge.Background = new SolidColorBrush(Color.FromRgb(254, 226, 226)); // #fee2e2
                badge.BorderBrush = new SolidColorBrush(Color.FromRgb(252, 165, 165)); // #fca5a5
                txt.Foreground = new SolidColorBrush(Color.FromRgb(185, 28, 28)); // #b91c1c
                txt.Text = LocalizationManager.GetString("Status_Faulted_Badge", "⚠ Lỗi (Error)");
                break;
            case ProcessStatus.Starting:
                badge.Background = new SolidColorBrush(Color.FromRgb(254, 249, 195)); // #fef9c3
                badge.BorderBrush = new SolidColorBrush(Color.FromRgb(253, 224, 71)); // #fde047
                txt.Foreground = new SolidColorBrush(Color.FromRgb(161, 98, 7)); // #a16207
                txt.Text = LocalizationManager.GetString("Status_Starting_Badge", "⏳ Starting...");
                break;
            default:
                badge.Background = new SolidColorBrush(Color.FromRgb(241, 245, 249)); // #f1f5f9
                badge.BorderBrush = new SolidColorBrush(Color.FromRgb(203, 213, 225)); // #cbd5e1
                txt.Foreground = new SolidColorBrush(Color.FromRgb(100, 116, 139)); // #64748b
                txt.Text = LocalizationManager.GetString("Status_Stopped_Badge", "○ Stopped");
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
            string errMsg = string.Format(LocalizationManager.GetString("Msg_BrowserError", "Không thể mở trình duyệt: {0}"), ex.Message);
            string errTitle = LocalizationManager.GetString("Msg_ErrorTitle", "Lỗi");
            MessageBox.Show(errMsg, errTitle, MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private async void AfterProjectCreated(string projectName, string projectDir, string successMessage)
    {
        RefreshVHosts();
        _serviceManager.ReloadVHosts();

        TxtQuickAppStatus.Text = $"✓ {successMessage} (Domain: {projectName}.test)";
        PnlQuickAppProgress.Visibility = Visibility.Visible;
        PbQuickApp.Value = 100;

        // Đợi 800ms để Nginx master hoàn tất reload các worker, đảm bảo khi mở trình duyệt vhost mới đã sẵn sàng
        await Task.Delay(800);

        // Mở trình duyệt nếu được chọn
        if (ChkOpenBrowser?.IsChecked == true)
        {
            string url = $"http://{projectName}.test";
            try
            {
                Process.Start(new ProcessStartInfo { FileName = url, UseShellExecute = true });
            }
            catch { }
        }

        // Mở thư mục dự án trong Explorer nếu được chọn
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
            TxtQuickAppStatus.Text = LocalizationManager.GetString("Msg_StartingServices", "Đang kích hoạt máy chủ Nginx, PHP và MariaDB MySQL...");
            await Task.Run(() => _serviceManager.StartAll());
            // Đợi MySQL ổn định nhận socket
            await Task.Delay(1000);
        }
    }

    private async void BtnCreateBlankPhp_Click(object sender, RoutedEventArgs e)
    {
        string name = TxtQuickAppName.Text.Trim();
        if (string.IsNullOrWhiteSpace(name))
        {
            MessageBox.Show(LocalizationManager.GetString("Msg_EnterProjectName", "Vui lòng nhập tên dự án!"),
                            LocalizationManager.GetString("Msg_NoticeTitle", "Thông báo"),
                            MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        bool createDb = ChkCreateDb.IsChecked ?? true;
        if (createDb)
        {
            await EnsureServicesRunningAsync();
        }

        if (_quickAppManager.CreateBlankPhpApp(name, createDb, out string projectDir))
        {
            AfterProjectCreated(name, projectDir, string.Format(LocalizationManager.GetString("Msg_DeleteSuccess", "Đã tạo dự án '{0}'"), name));
        }
        else
        {
            string existsMsg = string.Format(LocalizationManager.GetString("Msg_ProjectExists", "Thư mục dự án '{0}' đã tồn tại trong www/!"), name);
            MessageBox.Show(existsMsg, LocalizationManager.GetString("Msg_NoticeTitle", "Thông báo"), MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private async void BtnCreateLaravel_Click(object sender, RoutedEventArgs e)
    {
        string name = TxtQuickAppName.Text.Trim();
        if (string.IsNullOrWhiteSpace(name))
        {
            MessageBox.Show(LocalizationManager.GetString("Msg_EnterProjectName", "Vui lòng nhập tên dự án!"),
                            LocalizationManager.GetString("Msg_NoticeTitle", "Thông báo"),
                            MessageBoxButton.OK, MessageBoxImage.Information);
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
            TxtQuickAppStatus.Text = LocalizationManager.GetString("Msg_CreatingLaravel", "Đang khởi tạo dự án Laravel qua Composer...");

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
                TxtQuickAppStatus.Text = "✗ " + result.Message;
                MessageBox.Show(result.Message, LocalizationManager.GetString("Msg_ErrorTitle", "Lỗi tạo Laravel"), MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }
        catch (Exception ex)
        {
            TxtQuickAppStatus.Text = "✗ " + ex.Message;
            MessageBox.Show($"Lỗi trong quá trình tạo dự án: {ex.Message}", LocalizationManager.GetString("Msg_ErrorTitle", "Lỗi"), MessageBoxButton.OK, MessageBoxImage.Error);
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
            MessageBox.Show(LocalizationManager.GetString("Msg_EnterProjectName", "Vui lòng nhập tên dự án!"),
                            LocalizationManager.GetString("Msg_NoticeTitle", "Thông báo"),
                            MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        if (!_serviceManager.IsRunning)
        {
            await EnsureServicesRunningAsync();
        }

        if (_quickAppManager.CreateStaticWebApp(name, out string projectDir))
        {
            AfterProjectCreated(name, projectDir, string.Format(LocalizationManager.GetString("Msg_DeleteSuccess", "Đã tạo dự án '{0}'"), name));
        }
        else
        {
            string existsMsg = string.Format(LocalizationManager.GetString("Msg_ProjectExists", "Thư mục dự án '{0}' đã tồn tại trong www/!"), name);
            MessageBox.Show(existsMsg, LocalizationManager.GetString("Msg_NoticeTitle", "Thông báo"), MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private async void BtnCreateWordPress_Click(object sender, RoutedEventArgs e)
    {
        string name = TxtQuickAppName.Text.Trim();
        if (string.IsNullOrWhiteSpace(name))
        {
            MessageBox.Show(LocalizationManager.GetString("Msg_EnterProjectName", "Vui lòng nhập tên dự án!"),
                            LocalizationManager.GetString("Msg_NoticeTitle", "Thông báo"),
                            MessageBoxButton.OK, MessageBoxImage.Information);
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
            TxtQuickAppStatus.Text = LocalizationManager.GetString("Msg_CreatingWordPress", "Đang chuẩn bị gói WordPress từ wordpress.org...");

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
                TxtQuickAppStatus.Text = "✗ " + result.Message;
                MessageBox.Show(result.Message, LocalizationManager.GetString("Msg_ErrorTitle", "Lỗi tạo WordPress"), MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }
        catch (Exception ex)
        {
            TxtQuickAppStatus.Text = "✗ " + ex.Message;
            MessageBox.Show($"Lỗi trong quá trình tạo dự án: {ex.Message}", LocalizationManager.GetString("Msg_ErrorTitle", "Lỗi"), MessageBoxButton.OK, MessageBoxImage.Error);
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
            bool isEn = LocalizationManager.CurrentLanguage == "en";
            var exts = PhpExtensionManager.GetExtensions(_serviceManager.ActivePhp.RootPath, isEn);
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
            string confirmMsg = string.Format(LocalizationManager.GetString("Msg_ConfirmDelete"), project.Name, project.Domain, project.ProjectPath);
            string confirmTitle = LocalizationManager.GetString("Msg_DeleteTitle", "Xác nhận xóa dự án");

            var confirm = MessageBox.Show(
                confirmMsg,
                confirmTitle,
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (confirm == MessageBoxResult.Yes)
            {
                try
                {
                    // 1. Xóa file cấu hình Nginx VHost nếu có
                    string confPath = Path.Combine(_serviceManager.VHostGen.NginxSitesEnabledDir, $"auto.{project.Domain}.conf");
                    if (File.Exists(confPath))
                    {
                        try { File.Delete(confPath); } catch { }
                    }

                    // 2. Xóa thư mục dự án trong www/
                    if (Directory.Exists(project.ProjectPath))
                    {
                        Directory.Delete(project.ProjectPath, recursive: true);
                    }

                    // 3. Tải lại danh sách và đồng bộ VHosts
                    RefreshVHosts();
                    await Task.Run(() => _serviceManager.ReloadVHosts());

                    string successMsg = string.Format(LocalizationManager.GetString("Msg_DeleteSuccess", "Đã xóa dự án '{0}' thành công!"), project.Name);
                    string noticeTitle = LocalizationManager.GetString("Msg_NoticeTitle", "Thông báo");
                    MessageBox.Show(successMsg, noticeTitle, MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    string errMsg = string.Format(LocalizationManager.GetString("Msg_DeleteError", "Không thể xóa dự án: {0}"), ex.Message);
                    string errTitle = LocalizationManager.GetString("Msg_ErrorTitle", "Lỗi");
                    MessageBox.Show(errMsg, errTitle, MessageBoxButton.OK, MessageBoxImage.Error);
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
            // Thay vì tắt hẳn, ẩn vào Khay hệ thống
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

        // 1. Ẩn ngay TrayIcon ở khay hệ thống để tạo phản hồi tức thì
        try
        {
            TrayIcon.Visibility = Visibility.Collapsed;
            TrayIcon.Dispose();
        }
        catch { }

        // 2. Ẩn cửa sổ ngay
        try
        {
            Hide();
        }
        catch { }

        // 3. Dừng dịch vụ trên background task với timeout 1.5 giây
        try
        {
            await Task.Run(() =>
            {
                _serviceManager.StopAll();
                _serviceManager.Dispose();
            }).WaitAsync(TimeSpan.FromMilliseconds(1500));
        }
        catch { }

        // 4. Quét sạch các tiến trình con Novagon còn sót lại
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
        bool isEn = LocalizationManager.CurrentLanguage == "en";
        var packages = _packageManager.GetAvailablePackages();
        foreach (var p in packages)
        {
            if (isEn)
            {
                p.DisplayName = p.DisplayName
                    .Replace("(Khuyến nghị, NTS x64)", "(Recommended, NTS x64)")
                    .Replace("(LTS ổn định, NTS x64)", "(LTS Stable, NTS x64)")
                    .Replace("(Tương thích MySQL 8.0, x64)", "(MySQL 8.0 Compatible, x64)");
                p.CustomActionText = p.IsInstalled ? "✓ Installed" : "Download (1-Click)";
            }
            else
            {
                p.CustomActionText = p.IsInstalled ? "✓ Đã cài đặt" : "Tải về (1-Click)";
            }
        }
        ListPackages.ItemsSource = packages;
    }

    private async void BtnDownloadPackage_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is AvailablePackage pkg)
        {
            btn.IsEnabled = false;
            PnlDownloadProgress.Visibility = Visibility.Visible;
            PbDownload.Value = 0;
            TxtDownloadStatus.Text = string.Format(LocalizationManager.GetString("Msg_DownloadingPkg", "Bắt đầu tải {0}..."), pkg.DisplayName);

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
                TxtDownloadStatus.Text = $"✓ {result.Message}";
                LoadRuntimes();
                RefreshPackageList();
            }
            else
            {
                TxtDownloadStatus.Text = $"✗ " + result.Message;
                btn.IsEnabled = true;
            }
        }
    }
}
