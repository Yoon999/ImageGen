using System.IO;

namespace ImageGen.Helpers;

public static class Logger
{
    private static readonly object _lock = new object();
    private static readonly string _logDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Logs");

    public static void LogError(string message, Exception? ex = null)
    {
        WriteLog("ERROR", message, ex);
    }

    public static void LogInfo(string message)
    {
        WriteLog("INFO", message, null);
    }

    private static void WriteLog(string level, string message, Exception? ex)
    {
        try
        {
            lock (_lock)
            {
                if (!Directory.Exists(_logDirectory))
                {
                    Directory.CreateDirectory(_logDirectory);
                }

                string fileName = $"log_{DateTime.Now:yyyyMMdd}.txt";
                string filePath = Path.Combine(_logDirectory, fileName);
                
                string logContent = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] [{level}] {message}";
                
                if (ex != null)
                {
                    logContent += $"{Environment.NewLine}Exception: {ex.GetType().Name}";
                    logContent += $"{Environment.NewLine}Message: {ex.Message}";
                    logContent += $"{Environment.NewLine}StackTrace:{Environment.NewLine}{ex.StackTrace}";
                    
                    if (ex.InnerException != null)
                    {
                        logContent += $"{Environment.NewLine}Inner Exception: {ex.InnerException.Message}";
                    }
                }

                logContent += $"{Environment.NewLine}--------------------------------------------------{Environment.NewLine}";

                File.AppendAllText(filePath, logContent);
            }
        }
        catch (Exception)
        {
            // 로깅 중 에러 발생 시 무시 (무한 루프 방지)
            // 디버그 모드에서는 콘솔 출력
            System.Diagnostics.Debug.WriteLine($"Failed to write log: {message}");
        }
    }
}
