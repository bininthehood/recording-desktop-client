using RecordClient.Helpers;
using RecordClient.Services.Record;
using RecordClient.Services.Sending;
using System.Collections.Concurrent;
using System.IO;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Cryptography.X509Certificates;
using System.Text;


namespace RecordClient.Services.Server
{
    public class TCPSendData
    {
        private static readonly string TAG = "TCPSendData";

        private Thread? senderThread = null;
        private bool senderRunning = false;

        private TcpClient? senderSocket = null;
        private SslStream? senderSSLSocket = null;

        private BufferedStream? senderInput = null;
        private BufferedStream? senderOutput = null;
        private BlockingCollection<Message>? senderQueue = null;

        private int tlsMode = 0;

        private string serverIp = "";
        private int serverPort = 0;

        private bool aliveCheck = true;
        private DateTime sendTime = new DateTime();
        private int errorCount = 0;

        private VoiceManager? m_voiceManager = null; // 음성 관리 객체

        public TCPSendData(string ip, int port, bool aliveCheck, VoiceManager? vm)
        {
            serverIp = ip;
            serverPort = port;
            this.aliveCheck = aliveCheck;
            senderQueue = new BlockingCollection<Message>(30000);

            if (vm != null) m_voiceManager = vm;
        }

        public bool isConnected => senderRunning;

        public bool Start()
        {
            if (senderRunning)
            {
                return true;
            }

            Clear();

            senderRunning = true;
            senderThread = new Thread(new ThreadStart(Run));

            if (senderThread == null)
            {
                senderRunning = false;
                return false;
            }

            senderThread.Start();

            Logger.Info($"[{TAG}] 서버 연결 시작!");

            return true;
        }

        public bool Stop()
        {
            if (!senderRunning)
            {
                return true;
            }

            senderRunning = false;

            if (senderThread != null)
            {
                try
                {
                    senderThread.Join();
                }
                catch (ThreadInterruptedException e)
                {
                    Logger.Error($"[{TAG}] 스레드 중지 중 오류 발생: {e.Message}");
                }
                senderThread = null;
            }

            Logger.Info($"[{TAG}] 서버 연결 종료!");

            return true;
        }

        public bool Clear()
        {
            if (senderSocket != null)
            {
                try
                {
                    if (senderSocket.Connected)
                    {
                        senderSocket.Close();
                    }
                    senderSocket = null;
                }
                catch (IOException e)
                {
                    Logger.Error($"[{TAG}] 소켓 나가기 중 오류 발생: {e.Message}");
                }

                Logger.Info($"[{TAG}] 서버 연결 정리 완료!");
            }

            if (senderSSLSocket != null)
            {
                try
                {
                    senderSSLSocket.Close();
                    senderSSLSocket.Dispose();
                    senderSSLSocket = null;
                }
                catch (IOException) { }
            }

            if (senderInput != null)
            {
                lock (senderInput)
                {
                    try
                    {
                        senderInput.Close();
                        senderInput.Dispose();
                        senderInput = null;
                    }
                    catch (IOException) { }
                }
            }

            if (senderOutput != null)
            {
                lock (senderOutput)
                {
                    try
                    {
                        senderOutput.Close();
                        senderOutput.Dispose();
                        senderOutput = null;
                    }
                    catch (IOException) { }
                }
            }

            return true;
        }


        public void Run()
        {
            Message message = null;

            CheckConnection();

            // 수신 스레드 시작
            Thread receiveThread = new Thread(ReceiveLoop);
            receiveThread.Start();

            while (senderRunning)
            {
                if (message == null)
                {
                    try
                    {
                        senderQueue.TryTake(out message, TimeSpan.FromMilliseconds(500));
                    }
                    catch (Exception e)
                    {
                        Logger.Error($"[{TAG}] 메시지 처리 중 오류 발생: {e.Message}");
                        Thread.Sleep(1000); // 너무 빨리 종료되지 않도록 대기
                        continue; // 루프 유지
                    }
                }

                if (message == null)
                {
                    CheckConnection();
                }
                else
                {
                    SendData(message);
                    message = null;
                }
            }
            Clear();
        }

        private void ReceiveLoop()
        {
            string serverInfo = serverIp + ":" + serverPort;
            byte[] buffer = new byte[4096];

            while (senderRunning && senderSocket != null && senderSocket.Connected)
            {
                try
                {
                    NetworkStream? stream = senderSocket.GetStream();
                    if (stream == null || !stream.CanRead)
                        break;

                    if (stream.DataAvailable)
                    {
                        int readBytes = stream.Read(buffer, 0, buffer.Length);
                        if (readBytes > 0)
                        {
                            string responseString = Encoding.UTF8.GetString(buffer, 0, readBytes);
                            Logger.Info($"[{TAG}] 서버 응답 수신: {responseString}");

                            string type = responseString.Substring(0, 2);
                            string code = responseString.Substring(2, 3);

                            switch (type)
                            {
                                case "01":
                                    {

                                        if (code == "200")
                                        {

                                            Logger.Info($"[{TAG}] 녹취 시작 완료!");
                                            if (m_voiceManager != null) m_voiceManager.PushInfo(serverInfo, "START_RECORD", "");
                                        }
                                        else
                                        {
                                            Logger.Info($"[{TAG}] 녹취 시작 실패!");
                                        }
                                    }
                                    break;

                                case "02":
                                    {
                                        if (code == "200")
                                        {
                                            Logger.Info($"[{TAG}] 녹취 종료 완료!");

                                            string filePath = responseString.Substring(5, 100);
                                            string fileName = responseString.Substring(105, 100);

                                            filePath = filePath.Trim();
                                            fileName = fileName.Trim();

                                            if (m_voiceManager != null) m_voiceManager.PushInfo(serverInfo, "STOP_RECORD", filePath + fileName);
                                            
                                        }
                                        else
                                        {
                                            Logger.Info($"[{TAG}] 녹취 종료 실패!");
                                        }
                                    }
                                    break;

                                case "61":
                                    {
                                        string filePath = responseString.Substring(5, 100);
                                        string fileName = responseString.Substring(105, 100);

                                        filePath = filePath.Trim();
                                        fileName = fileName.Trim();

                                        Logger.Info($"[{TAG}] 녹취 파일 생성! - {filePath}{fileName}");

                                        if (m_voiceManager != null) m_voiceManager.PushInfo(serverInfo, "CREATE_FILE", filePath + fileName);
                                    }
                                    break;

                                case "62":
                                    {
                                        string filePath = responseString.Substring(5, 100);
                                        string fileName = responseString.Substring(105, 100);

                                        filePath = filePath.Trim();
                                        fileName = fileName.Trim();

                                        Logger.Info($"[{TAG}] 녹취 파일 종료! - {filePath}{fileName}");

                                        m_voiceManager.PushInfo(serverInfo, "FINISH_FILE", filePath + fileName);
                                    }
                                    break;
                            }

                        }
                    }
                    else
                    {
                        Thread.Sleep(50);
                    }
                }
                catch (IOException ioex)
                {
                    Logger.Error($"[{TAG}] 수신 중 IO 오류: {ioex.Message}");
                    break;
                }
                catch (Exception ex)
                {
                    Logger.Error($"[{TAG}] 수신 중 일반 오류: {ex.Message}");
                    break;
                }
            }

            Logger.Warn($"[{TAG}] 수신 루프 종료됨.");
        }

        private bool SendData(Message message)
        {
            byte[] packet = message.GetByte();

            if (packet == null || packet.Length == 0)
            {
                return true;
            }
            return SendData(packet);
        }

        private bool SendData(byte[] packet)
        {
            if (senderOutput == null)
            {
                return false;
            }

            try
            {
                senderOutput.Write(packet, 0, packet.Length);
                senderOutput.Flush();
                sendTime = DateTime.Now;

                errorCount = 0;
            }
            catch (IOException e)
            {
                Logger.Error($"[{TAG}] 입출력 예외 발생: {e.Message}");

                Clear();
                CheckConnection();

                return false;
            }
            catch (Exception e)
            {
                Logger.Error($"[{TAG}] 데이터 전송 오류: {e.Message}");

                Clear();
                CheckConnection();

                return false;
            }

            return true;
        }

        public bool SendMessage(Message message)
        {
            try
            {
                // 1000ms(1초) 동안 메시지를 추가 시도
                if (!senderQueue.TryAdd(message, TimeSpan.FromMilliseconds(1000)))
                {
                    Logger.Error($"[{TAG}] 전송 큐 추가 실패: 대기열이 가득 참");
                    return false;
                }
            }
            catch (Exception e)
            {
                Logger.Error($"[{TAG}] 전송 큐 추가 중 오류 발생: {e.Message}");
                return false;
            }

            return true;
        }

        public bool CheckConnection()
        {
            // 기존 연결이 유지되고 있다면 활성 상태 확인
            if (senderSocket != null || senderSSLSocket != null)
            {
                return AliveCheck();
            }

            // 연결이 없으면 새로운 연결 시도
            return Connection();
        }

        public bool AliveCheck()
        {
            // 헬스 체크 활성화 시
            if (sendTime != null && senderOutput != null && aliveCheck)
            {
                DateTime current = DateTime.Now;
                long diff = (long)(current - sendTime).TotalMilliseconds;

                // 30초 이상 경과하면 헬스 체크 메시지 전송
                if (diff >= 30000)
                {
                    MessageAlive message = new MessageAlive();
                    SendData(message);
                }
            }

            if (senderInput != null)
            {
                int count = 0;
                try
                {
                    while (true)
                    {
                        if (count <= 0)
                        {
                            break;
                        }
                        senderInput.Seek(count, SeekOrigin.Current); // 데이터 건너뛰기
                    }
                }
                catch (IOException e)
                {
                    if (errorCount++ % 10 == 0)
                    {
                        Logger.Error($"[{TAG}] 입력 스트림 오류: {e.Message}");

                        if (!aliveCheck)
                        {
                            Stop(); // 종료 처리
                        }
                        else
                        {
                            Clear();
                            CheckConnection();
                        }
                        return false;
                    }
                }
            }

            return true;
        }

        public bool OnServerCheck()
        {
            string senderInfo = $"{serverIp}:{serverPort}";
            try
            {
                Logger.Info($"[{TAG}] 소켓 연결 시도... {senderInfo}");

                using (var client = new TcpClient())
                {
                    client.ReceiveTimeout = 5000;
                    client.SendTimeout = 5000;
                    try
                    {
                        client.Connect(serverIp, serverPort);
                    }
                    catch (SocketException e)
                    {
                        Logger.Error($"[{TAG}] 소켓 연결 실패! - {e.Message}");
                        return false;
                    }
                }

                Logger.Info($"[{TAG}] 소켓 연결 정상");

                return true;
            }
            catch (Exception e)
            {
                Logger.Error($"[{TAG}] 소켓 연결 실패! - {e.Message}");

                return false;
            }
        }


        public bool CreateSocket()
        {
            string senderInfo = $"{serverIp}:{serverPort}";

            try
            {
                Logger.Info($"[{TAG}] TCP 연결 시도... {senderInfo}");

                TcpClient _socket = new TcpClient
                {
                    ReceiveTimeout = 5000, // 수신 시간 제한 (5초)
                    SendTimeout = 5000
                };

                _socket.Connect(serverIp, serverPort);

                NetworkStream _networkStream = _socket.GetStream();

                Logger.Info($"[{TAG}] TCP 연결 성공!");

                senderOutput = new BufferedStream(_networkStream);
                senderSocket = _socket;
                errorCount = 0;


                return true;
            }
            catch (Exception e)
            {
                Logger.Error($"[{TAG}] TCP 연결 실패... {e.Message}");

                if (errorCount++ % 10 == 0)
                {
                    Logger.Error($"[{TAG}] TCP 연결 실패! 오류: {e.Message}");

                    if (!aliveCheck)
                    {
                        Stop(); // 종료 처리
                    }

                    return false;
                }
                return false;
            }
        }

        public bool CreateSSLSocket()
        {
            TcpClient _sslClient = null;
            SslStream _sslStream = null;

            string senderInfo = $"{serverIp}:{serverPort}";
            Logger.Info($"[{TAG}] SSL 연결 시도... {senderInfo}");

            try
            {
                _sslClient = new TcpClient();
                _sslClient.Connect(new IPEndPoint(IPAddress.Parse(serverIp), serverPort));

                // SSL 인증서 검증을 무시하는 콜백 (테스트 환경에서만 사용)
                RemoteCertificateValidationCallback validationCallback = (sender, certificate, chain, sslPolicyErrors) => true;

                // SSL 스트림 생성 및 인증서 적용
                _sslStream = new SslStream(_sslClient.GetStream(), false, validationCallback);
                _sslStream.AuthenticateAsClient(serverIp);

                Logger.Info($"[{TAG}] SSL 연결 성공!");

                // TLS 프로토콜 확인
                Logger.Info($"[{TAG}] 사용된 TLS 프로토콜: {_sslStream.SslProtocol}");

                // 암호화 스위트 확인 (C#에서는 직접 설정 불가능, 지원 목록만 확인 가능)
                Logger.Info($"[{TAG}] 암호화 강도: {_sslStream.CipherStrength}");

                // @TODO
                /*this.senderInput = _sslStream;
                this.senderOutput = _sslStream;*/
                senderSSLSocket = _sslStream;
                errorCount = 0;

                return true;
            }
            catch (Exception e)
            {
                Logger.Error($"[{TAG}] SSL 연결 실패... {e.Message}");

                if (errorCount++ % 10 == 0)
                {
                    Logger.Error($"[{TAG}] SSL 연결 실패! 오류: {e.Message}");

                    if (!aliveCheck)
                    {
                        Stop(); // 종료 처리
                    }

                    return false;
                }
                return false;
            }
        }

        private bool Connection()
        {
            if (serverIp.Length > 0 && serverPort != 0)
            {
                string senderInfo = serverIp + ":" + serverPort;

                sendTime = DateTime.Now;
                if (tlsMode == 0)
                {
                    // 여기
                    return CreateSocket();
                }
                else
                {
                    return CreateSSLSocket();
                }
            }

            return false;
        }

        private X509Certificate2 LoadCertificate()
        {
            try
            {
                // @TODO
                byte[] certData = null;// Properties.Resources.MyCertificate;
                return new X509Certificate2(certData);
            }
            catch (Exception e)
            {
                Logger.Error($"[{TAG}] SSL 인증서 로드 실패: {e.Message}");
                return null;
            }
        }
    }
}