using RecordClient.Helpers.Config;
using RecordClient.Helpers.Popup;
using RecordClient.Services.Login;
using RecordClient.Services.Record;
using RecordClient.ViewModels;
using System.Collections.Concurrent;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Net.WebSockets;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Unicode;

namespace RecordClient.Helpers.InterfaceSocket
{
    public class InterfaceSocketLevel
    {
        private ConcurrentDictionary<string, WebSocket> _clientSockets = new();


        public static class ResultCode
        {
            /// <summary>
            /// 성공
            /// </summary>
            public const int Success = 200;

            /// <summary>
            /// 성공
            /// </summary>
            public const int PartialSuccess = 200;

            /// <summary>
            /// 잘못된 요청
            /// </summary>
            public const int BadRequest = 400;

            /// <summary>
            /// 서비스를 찾을 수 없음
            /// </summary>
            public const int NotFound = 404;

            /// <summary>
            /// 내부 에러발생
            /// </summary>
            public const int InternalError = 500;

            /// <summary>
            /// 유효하지 않은 서비스
            /// </summary>
            public const int ServiceUnavailable = 503;

            /// <summary>
            /// 장치 초기화 오류
            /// </summary>
            public const int DeviceNotReady = 540;

            /// <summary>
            /// 장치 범위를 초과한 오류
            /// </summary>
            public const int VolumeOutOfRange = 541;

            /// <summary>
            /// 기능 미구현
            /// </summary>
            public const int NotImplemented = 501;
        }
        private readonly string HostServer = "http://127.0.0.1:19101/";


        private static readonly string TAG = typeof(InterfaceSocketLevel).Name;
        private static InterfaceSocketLevel? _instance;
        private static readonly object _lock = new();

        public bool _isRunning = false;
        private static VoiceManager? m_voiceManager = null;

        private HttpListener? _httpListener = null;

        public static InterfaceSocketLevel Instance
        {
            get
            {
                if (_instance == null)
                {
                    throw new InvalidOperationException("InterfaceSocketLevel은 Init()으로 먼저 초기화되어야 합니다.");
                }
                return _instance;
            }
        }

        public static void Init(VoiceManager voiceManager)
        {
            lock (_lock)
            {
                if (_instance == null)
                {

                    _instance = new InterfaceSocketLevel();
                    m_voiceManager = voiceManager;
                }
            }
        }

        public void Start()
        {
            if (_isRunning) return;
            _isRunning = true;

            Task.Run(async () =>
            {
                while (true)
                {
                    if (!_isRunning) break;
                    try
                    {
                        if (_httpListener != null)
                        {
                            try
                            {
                                _httpListener.Stop();
                                _httpListener.Close();
                            }
                            catch (Exception ex)
                            {
                                Logger.Error($"[{TAG}] 기존 리스너 정리 중 예외: {ex.Message}");
                            }
                            finally
                            {
                                _httpListener = null;
                            }
                        }

                        await RunServerAsync(); // 내부에서 Stop() 호출됨

                    }
                    catch (HttpListenerException ex)
                    {
                        Logger.Error($"[{TAG}] 포트 바인딩 실패: {ex.Message}");
                    }
                    catch (ObjectDisposedException)
                    {
                        Logger.Info($"[{TAG}] HttpListener가 이미 종료됨 (정상 종료)");
                    }
                    catch (Exception ex)
                    {
                        webSocketError($"[{TAG}] 웹 소켓 실행 중 오류 발생: {ex.Message}");
                    }

                    await Task.Delay(3000); // 재시도 간격
                }
            });
        }


        public void Stop()
        {
            _isRunning = false;

            // 연결된 클라이언트 소켓 모두 정리
            foreach (var kv in _clientSockets)
            {
                try
                {
                    if (kv.Value.State == WebSocketState.Open)
                        kv.Value.CloseAsync(WebSocketCloseStatus.NormalClosure, "Server shutting down", CancellationToken.None).Wait();
                }
                catch (Exception ex)
                {
                    Logger.Warn($"[{TAG}] 클라이언트 소켓 종료 실패: {ex.Message}");
                }
                finally
                {
                    kv.Value.Dispose();
                }
            }
            _clientSockets.Clear();

            if (_httpListener != null)
            {
                try
                {
                    if (_httpListener.IsListening)
                    {
                        _httpListener.Stop();
                    }
                    _httpListener.Close();
                }
                catch (Exception ex)
                {
                    Logger.Error($"[{TAG}] Stop 중 예외: {ex.Message}");
                }
                finally
                {
                    _httpListener = null;
                }
            }
        }



        private async Task RunServerAsync()
        {
            _httpListener = new HttpListener();
            _httpListener.Prefixes.Add(HostServer);
            _httpListener.Start();

            Logger.Info($"[{TAG}] 웹 소켓 서버 시작! - {HostServer}");

            while (_isRunning)
            {
                if (_httpListener == null || !_httpListener.IsListening)
                    break;

                HttpListenerContext context;
                try
                {
                    context = await _httpListener.GetContextAsync();
                }
                catch (HttpListenerException)
                {
                    Logger.Info($"[{TAG}] 웹 소켓 서버 종료  - {HostServer}");
                    break;
                }
                catch (ObjectDisposedException)
                {
                    Logger.Info($"[{TAG}] 웹 소켓 서버 종료  - {HostServer}");
                    break;
                }

                if (context.Request.IsWebSocketRequest)
                {
                    _ = Task.Run(() => HandleClientAsync(context));
                }
                else
                {
                    context.Response.StatusCode = ResultCode.BadRequest;
                    context.Response.Close();
                    Logger.Info($"[{TAG}] 웹 소켓 서버 종료  -  {HostServer}");
                }
            }

            Stop(); // 자원 정리
        }
        private async Task SendLevelLoop(string clientId, WebSocket socket)
        {
            while (socket.State == WebSocketState.Open)
            {
                try
                {
                    if (m_voiceManager != null)
                    {
                        float lv = m_voiceManager.GetLevel();

                        // 원하는 형식으로 전송: 문자열 or JSON 등
                        string lvStr = lv.ToString();
                        byte[] resBytes = Encoding.UTF8.GetBytes(lvStr);

                        await socket.SendAsync(new ArraySegment<byte>(resBytes), WebSocketMessageType.Text, true, CancellationToken.None);
                        await Task.Delay(20);
                    }
                }
                catch (Exception ex)
                {
                    Logger.Error($"[{TAG}] Send Level Loop 예외: {ex.Message}");
                    break;
                }
            }

            Logger.Info($"[{TAG}] Send Level Loop 종료: {clientId}");
        }

        private async Task HandleClientAsync(HttpListenerContext context)
        {
            WebSocketContext wsContext = await context.AcceptWebSocketAsync(null);
            WebSocket webSocket = wsContext.WebSocket;


            string clientId = Guid.NewGuid().ToString(); // 고유 식별자
            _clientSockets.TryAdd(clientId, webSocket);

            Logger.Info($"[{TAG}] ------------- 클라이언트 연결");


            try
            {
                var sendTask = Task.Run(async () => await SendLevelLoop(clientId, webSocket));


                byte[] buffer = new byte[4096];

                while (webSocket.State == WebSocketState.Open)
                {
                    var result = await webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);

                    if (result.MessageType == WebSocketMessageType.Close)
                    {
                        break;
                    }
                }

            }
            catch (WebSocketException ex)
            {
                webSocketError($"웹소켓 예외 발생: {ex.Message}");
            }
            finally
            {
                if (webSocket.State == WebSocketState.Open)
                {
                    await webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Bye", CancellationToken.None);
                }
                webSocket.Dispose();
                Logger.Info($"[{TAG}] 클라이언트 종료됨: {clientId}");
            }

            webSocket.Dispose();
        }



        private void webSocketSuccess(string suc)
        {

            Logger.Info(suc);
            Alert.Show("INFO", suc);
        }

        private void webSocketError(string err)
        {

            Alert.Show("ERROR", err);
            Logger.Error(err);
        }

    }

}