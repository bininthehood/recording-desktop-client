using RecordClient.Helpers;
using RecordClient.Models.Login;
using System.IO;
using System.Text.Json;

namespace RecordClient.Services.Login
{
    public static class LoginConfigService
    {
        private static readonly string ConfigPath = Path.Combine(AppContext.BaseDirectory);
        private static readonly string FilePath = Path.Combine(ConfigPath, ".sys.db");

        public static void Save(LoginInfo info)
        {
            try
            {
                if (!Directory.Exists(ConfigPath))
                {
                    Directory.CreateDirectory(ConfigPath);
                    Logger.Info($"설정 디렉터리 생성 .. {ConfigPath}");
                }
                Logger.Debug($"ID: {info.UserId}");
                var encrypted = new LoginInfo
                {
                    UserId = AESService.Encrypt(info.UserId),
                    Password = AESService.Encrypt(info.Password)
                };

                var json = JsonSerializer.Serialize(encrypted);
                File.WriteAllText(FilePath, json);

                Logger.Info($"AES 로그인 정보 저장 완료 {ConfigPath}");
            }
            catch (Exception ex)
            {
                Logger.Error($"AES 로그인 정보 저장 실패 - {ex.Message}");
            }
        }

        public static LoginInfo? Load()
        {
            try
            {
                if (!File.Exists(FilePath))
                {
                    Logger.Error("로그인 설정 파일 없음");
                    return null;
                }

                var json = File.ReadAllText(FilePath);
                var encrypted = JsonSerializer.Deserialize<LoginInfo>(json);

                if (encrypted == null) return null;

                var decrypted = new LoginInfo
                {
                    UserId = AESService.Decrypt(encrypted.UserId),
                    Password = AESService.Decrypt(encrypted.Password)
                };

                Logger.Info("AES 로그인 정보 로딩 성공");
                return decrypted;
            }
            catch (Exception ex)
            {
                Logger.Error($"로그인 정보 로딩 실패: {ex.Message}");
                return null;
            }
        }
    }
}
