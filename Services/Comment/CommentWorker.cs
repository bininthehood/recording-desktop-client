using RecordClient.Helpers;
using RecordClient.Services.Sending;
using System.IO;
using System.Net.Sockets;
using System.Text;

public class CommentWorker
{


    private readonly string _host;
    private readonly int _port;

    public CommentWorker(string host, int port)
    {
        _host = host;
        _port = port;
    }


    public async Task<bool> Send(MessageCommentSend message)
    {
        try
        {
            using var client = new TcpClient();
            client.ReceiveTimeout = 5000;
            client.SendTimeout = 2000;

            await client.ConnectAsync(_host, _port);

            using NetworkStream stream = client.GetStream();
            using BufferedStream buffered = new BufferedStream(stream);


            byte[] packet = message.GetByte();

            // 데이터 송신
            await buffered.WriteAsync(packet, 0, packet.Length);
            await buffered.FlushAsync();

            // 데이터 수신
            byte[] buffer = new byte[5];
            int readBytes = await buffered.ReadAsync(buffer, 0, 5);

            string responseString = Encoding.UTF8.GetString(buffer, 0, readBytes);
            Logger.Info("서버 응답 수신: " + responseString);

            if (responseString.Length >= 5)
            {
                string type = responseString.Substring(0, 2);
                string code = responseString.Substring(2, 3);

                Logger.Info("Type : " + type);
                Logger.Info("Code : " + code);

                return code == "200";
            }

            return false;
        }
        catch (SocketException ex)
        {
            Logger.Error("서버 연결 실패: " + ex.Message);
            return false;
        }
        catch (IOException ex)
        {
            Logger.Error("서버 응답 읽기 실패 (IO): " + ex.Message);
            return false;
        }
        catch (Exception ex)
        {
            Logger.Error("서버 응답 읽기 실패 (일반): " + ex.Message);
            return false;
        }
    }

}
