using System;
using System.IO;
using System.Windows;
using Novagon.UI.Localization;

namespace Novagon.UI;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Khởi tạo hệ thống đa ngôn ngữ (đọc cài đặt lưu trước đó)
        LocalizationManager.Initialize();

        AppDomain.CurrentDomain.UnhandledException += (s, args) =>
        {
            try
            {
                File.AppendAllText("crash.log", $"[{DateTime.Now}] AppDomain Exception: {args.ExceptionObject}\n");
            }
            catch { }
        };

        DispatcherUnhandledException += (s, args) =>
        {
            try
            {
                File.AppendAllText("crash.log", $"[{DateTime.Now}] Dispatcher Exception: {args.Exception}\n");
                MessageBox.Show($"Đã xảy ra sự cố không mong muốn:\n{args.Exception.Message}\n\nChi tiết kỹ thuật đã được ghi lại trong file crash.log.", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Error);
                args.Handled = true;
            }
            catch { }
        };
    }
}
