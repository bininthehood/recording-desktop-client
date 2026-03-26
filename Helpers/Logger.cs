using System.IO;

namespace RecordClient.Helpers
{
    public static class Logger
    {
        private static StreamWriter? logWriter;
        private static readonly object lockObj = new();
        private static string? logPath;

        public static void Init()
        {
            try
            {
                string logDirectory = @"C:\VoiceLog\Service\Logs";
                string today = DateTime.Now.ToString("yyyy-MM-dd");
                string logFileName = $"AudioManager_{today}.log";
                logPath = Path.Combine(logDirectory, logFileName);

                if (!Directory.Exists(logDirectory))
                {
                    Directory.CreateDirectory(logDirectory);
                }

                logWriter = new StreamWriter(logPath, append: true)
                {
                    AutoFlush = true
                };

                Info("Logger Initiated !");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Logger Init Error] {ex.Message}");
            }
        }

        public static void Info(string message) => Write("[INFO]", message);
        public static void Error(string message) => Write("[ERROR]", message);
        public static void Debug(string message) => Write("[DEBUG]", message);
        public static void Warn(string message) => Write("[Warn]", message);

        private static void Write(string level, string message)
        {
            lock (lockObj)
            {
                try
                {
                    // logWriter가 null이거나 로그 파일이 삭제된 경우 재초기화
                    if (logWriter == null || (logPath != null && !File.Exists(logPath)))
                    {
                        Init();
                    }

                    string logMessage = $"{level} [{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {message}";
                    Console.WriteLine(logMessage);
                    logWriter?.WriteLine(logMessage);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[Logger Write Error] {ex.Message}");
                }
            }
        }

        public static void Close()
        {
            try
            {
                logWriter?.Close();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Logger Close Error] {ex.Message}");
            }
        }
    }
}