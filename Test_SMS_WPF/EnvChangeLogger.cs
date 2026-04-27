using System.IO;
using System.Text;

namespace Test_SMS_WPF;

internal static class EnvChangeLogger
{
    private static readonly object Sync = new();

    public static void Write(string message)
    {
        var fileName = $"test-sms-wpf-app-{DateTime.Now:yyyyMMdd}.log";
        var path = Path.Combine(AppContext.BaseDirectory, fileName);
        var line = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] {message}{Environment.NewLine}";
        lock (Sync)
        {
            File.AppendAllText(path, line, Encoding.UTF8);
        }
    }
}
