using System;
using System.IO;
using System.Windows;
using System.Windows.Threading;

namespace Novagon.UI;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        AppDomain.CurrentDomain.UnhandledException += (s, args) =>
        {
            File.AppendAllText("crash.log", $"[{DateTime.Now}] AppDomain Exception: {args.ExceptionObject}\n");
        };

        DispatcherUnhandledException += (s, args) =>
        {
            try
            {
                File.AppendAllText("crash.log", $"[{DateTime.Now}] Dispatcher Exception: {args.Exception}\n");
                MessageBox.Show($"ÄÃ£ xáº£y ra lá»—i:\n{args.Exception.Message}\n\nChi tiáº¿t xem trong crash.log", "Lá»—i á»©ng dá»¥ng", MessageBoxButton.OK, MessageBoxImage.Error);
                args.Handled = true;
            }
            catch
            {
            }
        };
    }
}


