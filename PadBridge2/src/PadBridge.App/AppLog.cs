using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace PadBridge.App;

internal static class AppLog
{
    private const int AttachParentProcess = -1;
    private static readonly object Sync = new();

    public static string LogDirectory { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "PadBridge",
        "logs");

    public static string LogFilePath { get; } = Path.Combine(
        LogDirectory,
        $"padbridge-{DateTime.Now:yyyyMMdd}.log");

    public static void AttachParentConsole()
    {
        AttachConsole(AttachParentProcess);
        try
        {
            var stdout = new StreamWriter(Console.OpenStandardOutput()) { AutoFlush = true };
            stdout.NewLine = Environment.NewLine;
            Console.SetOut(stdout);
            Console.OutputEncoding = Encoding.UTF8;
        }
        catch
        {
            // 親コンソールが無い場合はファイルログのみ
        }
    }

    public static void Info(string message) => Write("INFO", message, null);

    public static void Error(string message, Exception? exception = null) => Write("ERROR", message, exception);

    public static void Fatal(string message, Exception? exception = null) => Write("FATAL", message, exception);

    private static void Write(string level, string message, Exception? exception)
    {
        var builder = new StringBuilder();
        builder.Append(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff"));
        builder.Append(" [").Append(level).Append("] ").Append(message);
        if (exception is not null)
        {
            builder.AppendLine().Append(exception);
        }

        var line = builder.ToString();
        lock (Sync)
        {
            try
            {
                Console.WriteLine(line);
            }
            catch
            {
                // コンソールが無い場合はファイルのみ
            }

            try
            {
                Directory.CreateDirectory(LogDirectory);
                File.AppendAllText(LogFilePath, line + Environment.NewLine);
            }
            catch
            {
                // ログ書き込み失敗でさらに落とさない
            }
        }
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool AttachConsole(int dwProcessId);
}
