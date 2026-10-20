using System.Windows;

namespace PadBridge.App;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        AppLog.AttachParentConsole();
        AppLog.Info("PadBridge 起動");

        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
        {
            var exception = args.ExceptionObject as Exception
                            ?? new InvalidOperationException(args.ExceptionObject?.ToString() ?? "不明な致命的エラー");
            AppLog.Fatal("AppDomain.CurrentDomain.UnhandledException", exception);
            TryShowError("未処理の致命的エラーが発生しました。", exception);
        };

        TaskScheduler.UnobservedTaskException += (_, args) =>
        {
            AppLog.Error("TaskScheduler.UnobservedTaskException", args.Exception);
            args.SetObserved();
        };

        try
        {
            var app = new App();
            app.InitializeComponent();
            app.Run();
        }
        catch (Exception ex)
        {
            AppLog.Fatal("起動処理で未処理例外", ex);
            TryShowError("アプリの起動に失敗しました。", ex);
        }

        AppLog.Info("PadBridge 終了");
    }

    private static void TryShowError(string title, Exception exception)
    {
        try
        {
            MessageBox.Show(
                $"{title}\n\n{exception.Message}\n\n詳細はログに保存しました:\n{AppLog.LogFilePath}",
                "PadBridge",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
        catch
        {
            // メッセージボックス自体が使えない場合はログのみ
        }
    }
}
