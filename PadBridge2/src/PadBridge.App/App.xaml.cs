using System.Windows;
using System.Windows.Threading;

namespace PadBridge.App;

public partial class App : Application
{
    public App()
    {
        DispatcherUnhandledException += OnDispatcherUnhandledException;
    }

    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        AppLog.Error("DispatcherUnhandledException", e.Exception);
        MessageBox.Show(
            $"未処理例外が発生しましたが、アプリは継続します。\n\n{e.Exception.Message}\n\n詳細: {AppLog.LogFilePath}",
            "PadBridge",
            MessageBoxButton.OK,
            MessageBoxImage.Error);
        e.Handled = true;
    }
}
