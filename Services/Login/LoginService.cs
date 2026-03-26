using RecordClient.Helpers;
using RecordClient.Services.Sending;
using RecordClient.Services.Utils;
using System.IO;
using System.Net.Sockets;
using System.Text;

namespace RecordClient.Services.Login
{
    public static class LoginService
    {
        public static async Task<bool> Authenticate(string host, int port, string id, string password)
        {
            //await Task.Delay(1000);

            try
            {
                using var client = new TcpClient();

                try
                {
                    bool connected = await LinkToServerAsync(client, host, port);
                    if (!connected)
                    {
                        Logger.Error("서버 연결 실패: 연결 확인 불가");
                        return false;
                    }
                }
                catch (SocketException e)
                {
                    Logger.Error($"소켓 연결 실패: {e.Message}");
                    return false;
                }

                using NetworkStream networkStream = client.GetStream();
                using BufferedStream senderOutput = new BufferedStream(networkStream);

                MessageUserCheck message = new MessageUserCheck
                {
                    UserId = id,
                    UserPwd = StringUtil.ComputeSHA512(password)
                };

                byte[] packet = message.GetByte();

                // 데이터 송신
                await senderOutput.WriteAsync(packet, 0, packet.Length);
                await senderOutput.FlushAsync();

                // 데이터 수신
                int len = 5;
                byte[] buffer = new byte[len];
                int readBytes = await networkStream.ReadAsync(buffer, 0, len);

                string responseString = Encoding.UTF8.GetString(buffer, 0, readBytes);
                Logger.Info("서버 응답 수신: " + responseString);

                if (responseString.Length >= 5)
                {
                    string type = responseString.Substring(0, 2);
                    string code = responseString.Substring(2, 3);

                    if( client.Connected )
                    {
                        client.Dispose();
                    }

                    if (code == "200") return true;
                }
            }
            catch (IOException e)
            {
                Logger.Error($"입출력 예외 발생: {e.Message}");
                return false;
            }

            return false;
        }

        public static async Task<bool> LinkToServerAsync(TcpClient client, string host, int port)
        {
            if (string.IsNullOrEmpty(host))
                return false;

            try
            {
                client.ReceiveTimeout = 5000;
                client.SendTimeout = 2000;

                await client.ConnectAsync(host, port);
                return true;
            }
            catch (SocketException e)
            {
                Logger.Error($"소켓 연결 실패: {e.Message}");
                return false;
            }
        }
        public static bool LinkToServerSync(TcpClient client, string host, int port)
        {
            if (string.IsNullOrEmpty(host))
                return false;

            try
            {
                client.Connect(host, port);

                return true;
            }
            catch (SocketException e)
            {
                Logger.Error($"소켓 연결 실패: {e.Message}");
                return false;
            }
        }
    }
}
