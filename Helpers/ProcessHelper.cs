using System.Diagnostics;
using System.Text.RegularExpressions;

namespace RecordClient.Helpers
{
    public static class ProcessHelper
    {
        public static void KillProcessOnPort(int port)
        {
            var psi = new ProcessStartInfo
            {
                FileName = "cmd.exe",
                Arguments = $"/C netstat -aon | findstr :{port}",
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = Process.Start(psi);
            string output = process!.StandardOutput.ReadToEnd();
            process.WaitForExit();

            var lines = output.Split('\n', StringSplitOptions.RemoveEmptyEntries);
            foreach (var line in lines)
            {
                if (line.Contains("LISTENING"))
                {
                    var parts = Regex.Split(line.Trim(), @"\s+");
                    if (int.TryParse(parts[^1], out int pid) && pid != Process.GetCurrentProcess().Id)
                    {
                        try
                        {
                            Process.GetProcessById(pid).Kill();
                            Process.GetProcessById(pid).WaitForExit();
                            Logger.Info($"[Startup] 포트 {port} 사용 중인 PID {pid} 종료");
                        }
                        catch (Exception ex)
                        {
                            Logger.Error($"[Startup] 포트 종료 실패: {ex.Message}");
                        }
                    }
                }
            }
        }
    }
}