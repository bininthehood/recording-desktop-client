using NAudio.CoreAudioApi;
using System.Text;

namespace RecordClient.Services.Utils
{
    public static class Util
    {
        // 스레드 일시 정지 (ms 단위)
        public static void Sleep(long ms)
        {
            try
            {
                Thread.Sleep((int)ms);
            }
            catch (ThreadInterruptedException)
            {
                // 예외 무시
            }
        }
        // 전달받은 장치의 마스터볼륨을 가져온다
        public static float GetMasterVolume(MMDevice device)
        {
            try
            {
                return device.AudioEndpointVolume.MasterVolumeLevelScalar; // 0.0 ~ 1.0
            }
            catch (Exception)
            {

                return 0;
            }
        }
        // 현재 입력장치의 마스터볼륨을 가져온다
        public static double GetInputMasterVolume()
        {
            using var enumerator = new MMDeviceEnumerator();
            try
            {
                var device = enumerator.GetDefaultAudioEndpoint(DataFlow.Capture, Role.Multimedia);
                return device.AudioEndpointVolume.MasterVolumeLevelScalar; // 0.0 ~ 1.0
            }
            catch (Exception)
            {

                return 0;
            }
        }

        // 현재 출력장치의 마스터볼륨을 가져온다
        public static double GetOutputMasterVolume()
        {
            using var enumerator = new MMDeviceEnumerator();
            try
            {
                var device = enumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);
                return device.AudioEndpointVolume.MasterVolumeLevelScalar; // 0.0 ~ 1.0
            }
            catch (Exception)
            {
                return 0;
            }
        }

        // 문자열을 정수로 변환 (변환 실패 시 0 반환)
        public static int StrToInt(string paramString)
        {
            if (!string.IsNullOrEmpty(paramString))
            {
                try
                {
                    return int.Parse(paramString);
                }
                catch (FormatException)
                {
                    // 변환 실패 시 기본값 반환
                }
            }
            return 0;
        }

        // 숫자를 0으로 채워 지정된 길이로 변환
        public static string ZeroPadding(int value, int length)
        {
            return value.ToString().PadLeft(length, '0');
        }

        // 문자열을 공백으로 채워 지정된 길이로 변환
        public static string SpacePadding(string value, int length)
        {
            return value.PadRight(length, ' ');
        }

        // 정수를 4바이트 배열로 변환
        public static byte[] IntToByteArray(int value)
        {
            byte[] byteArray = new byte[4];
            byteArray[0] = (byte)(value >> 24);
            byteArray[1] = (byte)(value >> 16);
            byteArray[2] = (byte)(value >> 8);
            byteArray[3] = (byte)value;
            return byteArray;
        }

        // 4바이트 배열을 정수로 변환
        public static int ByteArrayToInt(byte[] bytes)
        {
            if (bytes == null || bytes.Length < 4)
            {
                throw new ArgumentException("입력된 바이트 배열이 4바이트보다 작습니다.");
            }

            return (bytes[0] & 0xFF) << 24 |
                   (bytes[1] & 0xFF) << 16 |
                   (bytes[2] & 0xFF) << 8 |
                   bytes[3] & 0xFF;
        }

        public static short[] byteToShort(byte[] bytes)
        {
            short[] shorts = new short[bytes.Length / 2];

            try
            {
                Buffer.BlockCopy(bytes, 0, shorts, 0, bytes.Length);
            }
            catch (Exception e)
            {
                shorts = new short[bytes.Length / 2];
            }
            return shorts;
        }
    }

    

    public class RandomStringGenerator
    {
        private static readonly char[] chars =
            "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789".ToCharArray();

        private static readonly Random random = new Random();

        public static string Generate(int length)
        {
            var sb = new StringBuilder();
            for (int i = 0; i < length; i++)
            {
                sb.Append(chars[random.Next(chars.Length)]);
            }
            return sb.ToString();
        }
    }

}
