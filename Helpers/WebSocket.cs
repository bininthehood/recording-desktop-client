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
    public class InterfaceSocket
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
        private readonly string HostServer = "http://127.0.0.1:19100/";


        private static readonly string TAG = typeof(InterfaceSocket).Name;
        private static InterfaceSocket? _instance;
        private static readonly object _lock = new();

        IniFile _ini = new IniFile(Path.Combine(AppContext.BaseDirectory, "config.ini"));

        public bool _isRunning = false;
        private static VoiceManager? m_voiceManager = null;
        private static AudioPlayer? m_audioPlayer = null;

        private HttpListener? _httpListener = null;
        private string _version = "1.0.01";
        private string _build = "20250527";

        public static InterfaceSocket Instance
        {
            get
            {
                if (_instance == null)
                {
                    throw new InvalidOperationException("InterfaceSocket은 Init()으로 먼저 초기화되어야 합니다.");
                }
                return _instance;
            }
        }

        public static void Init(VoiceManager voiceManager, AudioPlayer audioPlayer)
        {
            lock (_lock)
            {
                if (_instance == null)
                {

                    _instance = new InterfaceSocket();
                    m_voiceManager = voiceManager;
                    m_audioPlayer = audioPlayer;
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


        private async Task HandleClientAsync(HttpListenerContext context)
        {
            WebSocketContext wsContext = await context.AcceptWebSocketAsync(null);
            WebSocket webSocket = wsContext.WebSocket;


            string clientId = Guid.NewGuid().ToString(); // 고유 식별자
            _clientSockets.TryAdd(clientId, webSocket);

            Logger.Info($"[{TAG}] ------------- 클라이언트 연결");


            try
            {
                byte[] buffer = new byte[4096];

                while (webSocket.State == WebSocketState.Open)
                {
                    //var result = await webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
                    var result = await webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);

                    if (result.MessageType == WebSocketMessageType.Close)
                    {

                        break;
                    }

                    string msg = Encoding.UTF8.GetString(buffer, 0, result.Count);
                    Logger.Info($"[WebSocket] Request >>> {msg}");

                    string model = "", type = "", command = "";
                    JsonElement param = default;

                    try
                    {
                        using JsonDocument doc = JsonDocument.Parse(msg);
                        JsonElement root = doc.RootElement;

                        if (root.TryGetProperty("MODEL", out var modelEl)) model = modelEl.GetString()!;
                        if (root.TryGetProperty("TYPE", out var typeEl)) type = typeEl.GetString()!;
                        if (root.TryGetProperty("COMMAND", out var commandEl)) command = commandEl.GetString()!;
                        if (root.TryGetProperty("PARAM", out var paramEl) && paramEl.ValueKind == JsonValueKind.Object)
                            param = paramEl.Clone();
                    }
                    catch (JsonException ex)
                    {
                        webSocketError($"[WebSocket] JSON 파싱 실패: {ex.Message}");
                        continue;
                    }
                    catch (Exception e)
                    {
                        webSocketError($"[WebSocket] 수신 메세지 처리 실패: {e.Message}");
                        continue;
                    }

                    string resStr = await ProcessMessage(model, type, command, param);
                    Logger.Info($"[WebSocket] Response >>> {resStr}"); // 여기를 응답 받은 후로 조정

                    byte[] resBytes = Encoding.UTF8.GetBytes(resStr);
                    await webSocket.SendAsync(new ArraySegment<byte>(resBytes), WebSocketMessageType.Text, true, CancellationToken.None);
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

                /*UIHelper.RunVm<MainPageViewModel>(vm =>
                {
                    if (vm.IsRecording) vm.StopRecording();
                });*/
            }

            webSocket.Dispose();
        }

        private async Task<string> ProcessMessage(string model, string type, string command, JsonElement param)
        {
            string rs = "{}";

            if (type == "set")
            {
                rs = command switch
                {
                    "init" => SetInit(param),
                    "record" => await SetRecordStart(param),
                    "pause" => SetRecordPause(param),
                    "stop" => SetRecordStop(param),
                    "aud_upload" => SetAudioUpload(param),
                    "aud_start" => SetAudioStart(param),
                    "aud_pause" => SetAudioPause(param),
                    "aud_stop" => SetAudioStop(param),
                    "aud_speed" => SetAudioSpeed(param),
                    "push_digi" => SetPushDigi(param),
                    "capture_name" => SetCaptureName(param),
                    "capture_vol" => SetCaptureVol(param),
                    "capture_mute" => SetCaptureMute(param),
                    "render_name" => SetRenderName(param),
                    "render_vol" => SetRenderVol(param),
                    "render_mute" => SetRenderMute(param),
                    _ => rs
                };
            }
            else if (type == "get")
            {
                rs = command switch
                {
                    "aud_status" => GetAudStatus(),
                    "status" => GetStatus(),
                    "version" => GetVersion(),
                    "capture_list" => GetCaptureList(param),
                    "capture_name" => GetCaptureName(param),
                    "capture_vol" => GetCaptureVol(param),
                    "capture_mute" => GetCaptureMute(param),
                    "render_list" => GetRenderList(param),
                    "render_name" => GetRenderName(param),
                    "render_vol" => GetRenderVol(param),
                    "render_mute" => GetRenderMute(param),
                    _ => rs
                };
            }

            return string.Format("{{\"MODEL\":\"{0}\", \"TYPE\":\"{1}\", \"COMMAND\":\"{2}\", \"PARAM\":{3}}}", model, type, command, rs);
        }

        public void Broadcast(string model, string type, string command, object param)
        {
            string json = JsonSerializer.Serialize(new
            {
                MODEL = model,
                TYPE = type,
                COMMAND = command,
                PARAM = param
            }, new JsonSerializerOptions
            {
                Encoder = JavaScriptEncoder.Create(UnicodeRanges.All)
            });

            byte[] bytes = Encoding.UTF8.GetBytes(json);

            foreach (var kv in _clientSockets)
            {
                var socket = kv.Value;
                if (socket.State == WebSocketState.Open)
                {
                    try
                    {
                        _ = socket.SendAsync(
                            new ArraySegment<byte>(bytes),
                            WebSocketMessageType.Text,
                            true,
                            CancellationToken.None
                        );
                    }
                    catch (Exception ex)
                    {
                        Logger.Warn($"[Broadcast] {kv.Key} 전송 실패: {ex.Message}");
                        _clientSockets.TryRemove(kv.Key, out _);
                    }
                }
                else
                {
                    _clientSockets.TryRemove(kv.Key, out _);
                }
            }

            Logger.Info($"[Broadcast] >>> {json}");
        }

        /// <summary>
        /// 녹취 정보 초기화
        /// </summary>
        /// <param name="param">
        /// {"mode":"1","engine":["rvm.itray.co.kr:17001"],"aic":[],"reckey":"0000_20250528050042_7497CF3642BE4ACABEDE","subkey":"1","mix":"0","headset":"0","opt":""}
        /// mode - (string)
        /// engine - (List<string>)
        /// aic - (List<string>)
        /// reckey - 고유 키(string)
        /// subkey - 보조 키(string)
        /// mix - (string)
        /// headset - (string)
        /// opt - (string)
        /// </param>
        /// <returns>
        /// {"code": 200}
        /// </returns>
        private string SetInit(JsonElement param)
        {
            if (m_voiceManager == null) return $"{{\"code\":\"{ResultCode.NotFound}\"}}";

            try
            {
                m_voiceManager.m_isAudioOpen = false;
                m_voiceManager.m_status = 0;
                m_voiceManager.ResetRecData();

                var json = new JsonUtil(param);

                // 1. 엔진 데이터
                bool setEngines = SetupEngines(json.GetArray<string>("engine"));
                if (!setEngines)
                {
                    return $"{{\"code\":\"{ResultCode.BadRequest}\"}}";
                }

                // 2. 녹취 필수 데이터
                string nowdt = DateTime.Now.ToString("yyyyMMddHHmmss");

                string reckey = json.GetOrDefault<string>("reckey", "");    // 녹취 키
                string subkey = json.GetOrDefault<string>("subkey", "");    // 보조 키
                string filePath = "";
                string fileName = $"{nowdt}_{reckey}_{subkey}";             // 파일 명
                int fileFormat = 1;                                         // 파일 유형( 1: wav / 2: mp3 )        

                if (string.IsNullOrEmpty(reckey))
                {
                    Logger.Error("Rec Key 값이 누락되었습니다.");
                    return $"{{\"code\":\"{ResultCode.BadRequest}\"}}";
                }

                if (string.IsNullOrEmpty(subkey))
                {
                    Logger.Error("Sub Key 값이 누락되었습니다.");
                    return $"{{\"code\":\"{ResultCode.BadRequest}\"}}";
                }

                if (_ini != null)
                {
                    filePath = _ini.Get("Server", "Path"); // 파일 저장 경로
                    string _fileFormat = _ini.Get("Recorder", "Format");
                    fileFormat = int.Parse(_ini.Get("Recorder", "Format"));

                    if (filePath == "") filePath = "C:/VoiceLog/LocalData/Rec/";
                    if (_fileFormat == "") _fileFormat = "2";

                    fileFormat = int.Parse(_fileFormat);
                }

                if (fileFormat < 1)
                {
                    fileFormat = 2;
                }

                m_voiceManager.SetRequiredData(reckey, subkey, filePath, fileName, fileFormat); // 전역 변수 세팅


                // 3. 보조 데이터
                string mode = json.GetOrDefault<string>("mode", "");
                string userId = json.GetOrDefault<string>("userId", "");    // 사용자 정보
                string mix = json.GetOrDefault<string>("mix", "");
                string headset = json.GetOrDefault<string>("headset", "");
                string opt = json.GetOrDefault<string>("opt", "");
                string tls = json.GetOrDefault<string>("tls", "");
                int testConn = int.Parse(json.GetOrDefault<string>("testConn", "0"));
                var aics = json.GetArray<string>("aic");

                m_voiceManager.SetOptionalData(mode, userId, mix, headset, opt, tls, testConn, aics); // 전역 변수 세팅

                return $"{{\"code\":\"{ResultCode.Success}\"}}";
            }
            catch (Exception ex)
            {
                webSocketError($"{ex.Message}");
                return $"{{\"code\":\"{ResultCode.BadRequest}\"}}";
            }
        }

        private bool SetupEngines(List<string>? engines)
        {
            if (engines == null || engines.Count == 0)
            {
                Logger.Error("수신된 호스트 엔진 정보가 없습니다");
                return false;
            }

            Logger.Debug($"엔진 정보 수신 갯수 - {engines.Count}");

            for (int i = 0; i < engines.Count; i++)
            {
                var parts = engines[i].Split(':');
                
                string str = engines[i];
                Logger.Debug($"엔진 정보 수신 -  {(i+1)}번 {str}");

                if (parts.Length == 2 && int.TryParse(parts[1], out int port))
                {
                    if (m_voiceManager != null) m_voiceManager.SetEngineUrl(i + 1, parts[0], port);
                }
            }

            return true;
        }

        /// <summary>
        /// 마이크 장치 변경
        /// </summary>
        /// <param name="param">
        /// {"name": "RAYV MIC"} - 입력 장치(string)
        /// </param>
        /// <returns>
        /// {"code": 200, "name": "RAYV MIC"}
        /// </returns>
        private string SetCaptureName(JsonElement param)
        {
            string name = "";
            if (m_voiceManager == null) return $"{{\"code\":{ResultCode.NotFound},\"name\":\"{name}\"}}";


            int ret = 0;
            try
            {
                var json = new JsonUtil(param);
                if (json != null)
                {
                    name = json.GetOrDefault<string>("name", "");
                    if (!string.IsNullOrEmpty(name))
                    {
                        ret = m_voiceManager.SetCaptureName(name);
                    }
                    else
                    {
                        ret = ResultCode.BadRequest;
                    }
                }
                else
                {
                    ret = ResultCode.BadRequest;
                }
            }
            catch (Exception ex)
            {
                ret = ResultCode.InternalError;
                webSocketError(ex.Message);
            }

            return $"{{\"code\":{ret},\"name\":\"{name}\"}}";
        }

        /// <summary>
        /// 마이크 음량 조절
        /// </summary>
        /// <param name="param">
        /// {"vol": 50} - 입력 볼륨(int)
        /// </param>
        /// <returns>
        /// {"code": 200, "vol": 50}
        /// </returns>
        private string SetCaptureVol(JsonElement param)
        {
            int volume = -1;
            if (m_voiceManager == null) return $"{{\"code\":{ResultCode.NotFound},\"vol\":{volume}}}";


            int ret = 0;
            try
            {
                var json = new JsonUtil(param);
                if (json != null)
                {
                    volume = json.GetOrDefault<int>("vol", -1);
                    if (volume > -1)
                    {
                        ret = m_voiceManager.SetCaptureVol(volume);
                    }
                    else
                    {
                        ret = ResultCode.BadRequest;
                    }
                }
                else
                {
                    ret = ResultCode.BadRequest;
                }
            }
            catch (Exception ex)
            {
                ret = ResultCode.InternalError;
                webSocketError(ex.Message);
            }
            return $"{{\"code\":{ret},\"vol\":{volume}}}";
        }

        /// <summary>
        /// 입력 장치 음소거 on / off
        /// </summary>
        /// <param name="param">
        /// {"mute": 0} - 1: 음소거, 0: 음소거 해제
        /// </param>
        /// <returns>
        /// {"code": 200, "mute": 0}
        /// </returns>
        private string SetCaptureMute(JsonElement param)
        {
            int result = -1;
            if (m_voiceManager == null) return $"{{\"code\":{ResultCode.NotFound}}}";


            int ret = 0;
            try
            {
                var json = new JsonUtil(param);
                if (json != null)
                {
                    int mute = json.GetOrDefault<int>("mute", -1);
                    if (mute == 0 || mute == 1)
                    {
                        bool ok = m_voiceManager.SetCaptureMute(mute);
                        if (ok)
                        {
                            ret = ResultCode.Success;
                            result = mute;
                        }
                        else
                        {
                            ret = ResultCode.InternalError;
                        }
                    }
                    else
                    {
                        ret = ResultCode.BadRequest;
                    }
                }
                else
                {
                    ret = ResultCode.BadRequest;
                }
            }
            catch (Exception ex)
            {
                ret = ResultCode.InternalError;
                webSocketError(ex.Message);
                throw;
            }

            return $"{{\"code\":{ret},\"mute\":\"{result}\"}}";
        }

        /// <summary>
        /// 스피커 장치 변경
        /// </summary>
        /// <param name="param">
        /// {"name": "Realtek(R) Audio"} - 출력 장치(string)
        /// </param>
        /// <returns>
        /// {"code": 200"}
        /// </returns>
        private string SetRenderName(JsonElement param)
        {
            string name = "";
            if (m_voiceManager == null) return $"{{\"code\":{ResultCode.NotFound},\"name\":\"{name}\"}}";


            int ret = 0;
            try
            {
                var json = new JsonUtil(param);
                if (json != null)
                {
                    name = json.GetOrDefault<string>("name", "");
                    if (!string.IsNullOrEmpty(name))
                    {
                        ret = m_voiceManager.SetRenderName(name);
                    }
                    else
                    {
                        ret = ResultCode.BadRequest;
                    }
                }
                else
                {
                    ret = ResultCode.BadRequest;
                }
            }
            catch (Exception ex)
            {
                ret = ResultCode.InternalError;
                webSocketError(ex.Message);
            }
            return $"{{\"code\":{ret},\"name\":\"{name}\"}}";
        }

        /// <summary>
        /// 스피커 음량 조절
        /// </summary>
        /// <param name="param">
        /// {"vol": 50} - 출력 볼륨(int)
        /// </param>
        /// <returns>
        /// {"code": 200, "vol": 50}
        /// </returns>
        private string SetRenderVol(JsonElement param)
        {
            int volume = -1;
            if (m_voiceManager == null) return $"{{\"code\":{ResultCode.NotFound},\"vol\":{volume}}}";


            int ret = 0;
            try
            {
                var json = new JsonUtil(param);
                if (json != null)
                {
                    volume = json.GetOrDefault<int>("vol", -1);
                    if (volume > -1)
                    {
                        ret = m_voiceManager.SetRenderVol(volume);
                    }
                    else
                    {
                        ret = ResultCode.BadRequest;
                    }
                }
                else
                {
                    ret = ResultCode.BadRequest;
                }
            }
            catch (Exception ex)
            {
                ret = ResultCode.InternalError;
                webSocketError(ex.Message);
            }
            return $"{{\"code\":{ret},\"vol\":{volume}}}";
        }

        /// <summary>
        /// 출력 장치 음소거 on / off
        /// </summary>
        /// <param name="param">
        /// {"mute": 0} - 1: 음소거, 0: 음소거 해제
        /// </param>
        /// <returns></returns>
        private string SetRenderMute(JsonElement param)
        {
            int mute = -1;
            if (m_voiceManager == null) return $"{{\"code\":{ResultCode.NotFound}}}";


            int ret;
            try
            {
                var json = new JsonUtil(param);
                if (json != null)
                {
                    mute = json.GetOrDefault<int>("mute", -1);
                    if (mute == 0 || mute == 1)
                    {
                        bool ok = m_voiceManager.SetRenderMute(mute);
                        if (ok)
                        {
                            ret = ResultCode.Success;
                        }
                        else
                        {
                            ret = ResultCode.InternalError;
                        }
                    }
                    else
                    {
                        ret = ResultCode.BadRequest;
                    }
                }
                else
                {
                    ret = ResultCode.BadRequest;
                }
            }
            catch (Exception ex)
            {
                ret = ResultCode.InternalError;
                webSocketError(ex.Message);
                throw;
            }

            return $"{{\"code\":{ret},\"mute\":\"{mute}\"}}";
        }



        /// <summary>
        /// 녹음 시작
        /// </summary>
        /// <returns>
        /// 200:성공 / 501: 녹음 실패
        /// </returns>
        private async Task<string> SetRecordStart(JsonElement param)
        {
            if (m_voiceManager == null) return $"{{\"code\":{ResultCode.NotFound}}}";


            int ret = -1;
            try
            {
                Logger.Info($"[{TAG}] Start Recording ...");
                bool start = await m_voiceManager.StartRecordingAsync();


                UIHelper.RunVm<MainPageViewModel>(vm =>
                {
                    vm.StartRecordingCallback(start);
                });
                if (start)
                {
                    ret = ResultCode.Success;
                    Logger.Info($"[{TAG}] [*] Recording Started . : {ret}");
                }
                else
                {
                    ret = ResultCode.InternalError;
                    webSocketError($"[TAG] [*] Recording Error!!");
                }
            }
            catch (Exception)
            {
                ret = ResultCode.InternalError;
                throw;
            }


            return $"{{\"code\":{ret}}}";
        }
        private string SetRecordPause(JsonElement param)
        {
            if (m_voiceManager == null) return $"{{\"code\":{ResultCode.NotFound}}}";


            int ret = -1;
            try
            {
                var json = new JsonUtil(param);
                if (json != null)
                {
                    Logger.Info($"[{TAG}] Pause Recording ...");

                    bool stop = m_voiceManager.PauseRecording();

                    UIHelper.RunVm<MainPageViewModel>(vm =>
                    {
                        vm.StopRecordingCallback(stop);
                    });

                    if (stop)
                    {
                        ret = ResultCode.Success;
                        Logger.Info($"[{TAG}] [*] Recording Paused . : {ret}");
                    }
                    else
                    {
                        ret = ResultCode.InternalError;
                        webSocketError($"[TAG] [*] Pausing Error!!");
                    }
                }
                else
                {
                    ret = ResultCode.BadRequest;
                }
            }
            catch (Exception)
            {
                ret = ResultCode.InternalError;
                throw;
            }


            return $"{{\"code\":{ret}}}";
        }

        /// <summary>
        /// 녹음 중지
        /// </summary>
        /// <returns>
        /// {"code": 200"}
        /// </returns>
        private string SetRecordStop(JsonElement param)
        {
            if (m_voiceManager == null) return $"{{\"code\":{ResultCode.NotFound}}}";


            int ret = 0;

            bool stop = m_voiceManager.StopRecording();

            if (stop)
            {
                UIHelper.RunVm<MainPageViewModel>(vm =>
                {
                    vm.StopRecordingCallback(stop);
                });

                ret = ResultCode.Success;
                Logger.Info($"[{TAG}] [*] Recording stopped . : {ret}");
            }
            else
            {
                ret = ResultCode.InternalError;
                webSocketError($"[{TAG}] [*] Stop Recording Error !! : {ret}");
            }

            return $"{{\"code\":{ret}}}";
        }

        /// <summary>
        /// 오디오 플레이어 파일 업로드
        /// </summary>
        /// <returns>
        /// {"code": 200"}
        /// </returns>
        private string SetAudioUpload(JsonElement param)
        {
            if (m_audioPlayer == null)
                return $"{{\"code\":\"{ResultCode.NotFound}\"}}";


            int ret = 0;
            try
            {

                var json = new JsonUtil(param);

                string gid = json.GetOrDefault<string>("gid", "");
                string id = json.GetOrDefault<string>("id", "");
                string url = json.GetOrDefault<string>("url", "");
                var filenames = json.GetArray<string>("filenames");

                ret = m_audioPlayer.SetAudioUpload(gid, id, url, filenames);
            }
            catch (Exception ex)
            {
                webSocketError($"{ex}");
                return $"{{\"code\":\"{ResultCode.BadRequest}\"}}";
            }


            return $"{{\"code\":{ret}}}";
        }

        /// <summary>
        /// 오디오 플레이어 시작
        /// </summary>
        /// <returns>
        /// {"code": 200"}
        /// </returns>
        private string SetAudioStart(JsonElement param)
        {
            if (m_audioPlayer == null)
                return $"{{\"code\":\"{ResultCode.NotFound}\"}}";


            int ret = 0;
            try
            {

                var json = new JsonUtil(param);

                string gid = json.GetOrDefault<string>("gid", "");
                string id = json.GetOrDefault<string>("id", "");
                string url = json.GetOrDefault<string>("url", "");
                var filenames = json.GetArray<string>("filenames");

                ret = m_audioPlayer.SetAudioStart(gid, id, url, filenames);
            }
            catch (Exception ex)
            {
                webSocketError($"{ex}");
                return $"{{\"code\":\"{ResultCode.BadRequest}\"}}";
            }

            return $"{{\"code\":{ret}}}";
        }

        /// <summary>
        /// 오디오 플레이어 일시정지
        /// </summary>
        /// <returns>
        /// {"code": 200"}
        /// </returns>
        private string SetAudioPause(JsonElement param)
        {
            if (m_audioPlayer == null)
                return $"{{\"code\":\"{ResultCode.NotFound}\"}}";


            int ret = 0;
            try
            {
                ret = m_audioPlayer.SetAudioPause();
            }
            catch (Exception ex)
            {
                webSocketError($"{ex}");
                return $"{{\"code\":\"{ResultCode.BadRequest}\"}}";
            }


            return $"{{\"code\":{ret}}}";
        }

        /// <summary>
        /// 오디오 플레이어 일시정지 해제
        /// </summary>
        /// <returns>
        /// {"code": 200"}
        /// </returns>
        private string SetAudioRestart(JsonElement param)
        {
            if (m_audioPlayer == null)
                return $"{{\"code\":\"{ResultCode.NotFound}\"}}";


            int ret = 0;
            try
            {

                ret = m_audioPlayer.SetAudioRestart();
            }
            catch (Exception ex)
            {
                webSocketError($"{ex}");
                return $"{{\"code\":\"{ResultCode.BadRequest}\"}}";
            }


            return $"{{\"code\":{ret}}}";
        }

        /// <summary>
        /// 오디오 플레이어 중지
        /// </summary>
        /// <returns>
        /// {"code": 200"}
        /// </returns>
        private string SetAudioStop(JsonElement param)
        {
            if (m_audioPlayer == null)
                return $"{{\"code\":\"{ResultCode.NotFound}\"}}";


            int ret = 0;
            try
            {
                Logger.Info($"[{TAG}] Uploading Audio .. ");

                ret = m_audioPlayer.SetAudioStop();
            }
            catch (Exception ex)
            {
                webSocketError($"{ex}");
                return $"{{\"code\":\"{ResultCode.BadRequest}\"}}";
            }


            return $"{{\"code\":{ret}}}";
        }

        /// <summary>
        /// 오디오 플레이어 재생 속도 설정
        /// </summary>
        /// <returns>
        /// {"code": 200"}
        /// </returns>
        private string SetAudioSpeed(JsonElement param)
        {
            if (m_audioPlayer == null)
                return $"{{\"code\":\"{ResultCode.NotFound}\"}}";


            int ret = 0;
            try
            {

                var json = new JsonUtil(param);

                // 정의서에 파라미터 정보가 없습니다.
                // int speed = json.GetOrDefault<int>("speed", 0); 

                ret = m_audioPlayer.SetAudioSpeed(1); // 수정 후 인자 speed 로 변경
            }
            catch (Exception ex)
            {
                webSocketError($"{ex}");
                return $"{{\"code\":\"{ResultCode.BadRequest}\"}}";
            }


            return $"{{\"code\":{ret}}}";
        }

        /// <summary>
        /// 디지털 창구 메시지 전달
        /// </summary>
        /// <returns>
        /// {"code": 200"}
        /// </returns>
        private string SetPushDigi(JsonElement param)
        {
            if (m_voiceManager == null)
                return $"{{\"code\":\"{ResultCode.NotFound}\"}}";


            int ret = ResultCode.NotImplemented;
            try
            {

                var json = new JsonUtil(param);

                string data = json.GetOrDefault<string>("data", "");

                //ret = m_voiceManager.SetPushDigi(data);
            }
            catch (Exception ex)
            {
                webSocketError($"{ex}");
                return $"{{\"code\":\"{ResultCode.BadRequest}\"}}";
            }


            return $"{{\"code\":{ret}}}";
        }

        /// <summary>
        /// 스피커 볼륨 값 취득
        /// </summary>
        /// <returns>
        /// {"code": "200", "vol": 100}
        /// </returns>
        private string GetRenderVol(JsonElement param)
        {
            if (m_voiceManager == null) return $"{{\"code\":{ResultCode.NotFound},\"vol\":0}}";


            int ret = ResultCode.NotImplemented;
            int volPercent = m_voiceManager.GetRenderVol();
            if (volPercent < 0)
            {
                ret = ResultCode.ServiceUnavailable;
            }
            else
            {
                ret = ResultCode.Success;
            }
            return $"{{\"code\":{ret},\"vol\":{volPercent}}}";
        }

        /// <summary>
        /// 마이크 볼륨 값 취득
        /// </summary>
        /// <returns>
        /// {"code": "200", "vol": 100}
        /// </returns>
        private string GetCaptureVol(JsonElement param)
        {
            if (m_voiceManager == null) return $"{{\"code\":{ResultCode.NotFound},\"vol\":0}}";

            int ret = ResultCode.NotImplemented;
            int volPercent = m_voiceManager.GetCaptureVol();
            if (volPercent < 0)
            {
                ret = ResultCode.ServiceUnavailable;
            }
            else
            {
                ret = ResultCode.Success;
            }

            return $"{{\"code\":{ret},\"vol\":{volPercent}}}";
        }


        /// <summary>
        /// 버전 체크
        /// </summary>
        /// <returns>
        /// {"version": "1.0.1", "build": 1}
        /// </returns>
        private string GetVersion()
        {
            return string.Format("{{\"version\":\"{0}\", \"build\":\"{1}\"}}", _version, _build);
        }

        /// <summary>
        /// 현재 음성의 크기값 체크
        /// ******************* 사용안함 ********************
        /// </summary>
        /// <returns>
        /// {"code": "200", "level": "100"}
        /// </returns>
        private string GetMicLevel()
        {
            float val = m_voiceManager.GetLevel();
            return string.Format("{\"code\":200, \"level\":\"{0}\"}", val);
        }

        /// <summary>
        /// 오디오 플레이어 상태체크
        /// AudioPlayer.cs 상태 정보를 가져온다.
        /// </summary>
        /// <returns>
        /// {"code": "200", "audio": "recording"}
        /// </returns>
        private string GetAudStatus()
        {
            string str = "";
            if (m_audioPlayer == null)
                return $"{{\"code\":{ResultCode.NotFound},\"audio\":\"{str}\"}}";


            int ret = ResultCode.NotImplemented;
            try
            {
                str = m_audioPlayer.GetAudioStatus();
                ret = ResultCode.Success;
            }
            catch (Exception ex)
            {
                ret = ResultCode.InternalError;
                webSocketError($"{ex.StackTrace}");
            }

            return $"{{\"code\":{ret},\"audio\":\"{str}\"}}";
        }

        /// <summary>
        /// 모듈 상태체크
        /// VoiceManger.cs 상태 정보를 가져온다.
        /// </summary>
        /// <returns>
        /// {"code": "200", "audio": "recording"}
        /// </returns>
        private string GetStatus()
        {
            string str1 = "";
            string str2 = "";
            if (m_voiceManager == null)
                return $"{{\"code\":{ResultCode.NotFound},\"audio\":\"{str1}\",\"rec\":\"{str2}\"}}";


            int ret = ResultCode.NotImplemented;
            try
            {
                // 1. 클라이언트 장치 연결 상태
                string inputStatus = m_voiceManager.GetInputDeviceStatus();
                // 2. 녹취 상태
                string recStatus = m_voiceManager.GetRecordStatus();

                str1 = inputStatus; // "open", "pause", "close"
                str2 = recStatus; // "recording", "stop"

                if (string.IsNullOrEmpty(str1) || string.IsNullOrEmpty(str2))
                {
                    ret = ResultCode.DeviceNotReady;
                }
                else
                {
                    ret = ResultCode.Success;
                }
            }
            catch (Exception ex)
            {
                ret = ResultCode.InternalError;
                webSocketError($"{ex.StackTrace}");
            }

            return $"{{\"code\":{ret},\"audio\":\"{str1}\",\"rec\":\"{str2}\"}}";
        }


        /// <summary>
        /// 스피커 음소거 여부 반환
        /// </summary>
        /// <returns>
        /// {"code": "200", "mute": "0"} - 0:음소거 / 1:음소거 아님 / -1:예외발생
        /// </returns>
        private string GetRenderMute(JsonElement param)
        {
            int ret = ResultCode.NotImplemented;
            if (m_voiceManager == null) return $"{{\"code\":{ResultCode.NotFound},\"mute\":\"{ret}\"}}";


            ret = m_voiceManager.GetRenderMute();
            int code = ResultCode.Success;

            return $"{{\"code\":{code},\"mute\":\"{ret}\"}}";
        }
        /// <summary>
        /// 마이크 음소거 여부 반환
        /// </summary>
        /// <returns>
        /// {"code": "200", "mute": "0"} - 0:음소거 / 1:음소거 아님 / -1:예외발생
        /// </returns>
        private string GetCaptureMute(JsonElement param)
        {
            int ret = ResultCode.NotImplemented;
            if (m_voiceManager == null) return $"{{\"code\":{ResultCode.NotFound},\"mute\":\"{ret}\"}}";


            ret = m_voiceManager.GetCaptureMute();
            int code = ResultCode.Success;

            return $"{{\"code\":{code},\"mute\":\"{ret}\"}}";
        }

        /// <summary>
        /// 출력장치 리스트 반환
        /// </summary>
        /// <returns>
        /// {"code": "200", "list": ["spk1", "spk2"]}
        /// </returns>
        private string GetRenderList(JsonElement param)
        {
            List<string>? renderList = null;
            var options = new JsonSerializerOptions
            {
                Encoder = JavaScriptEncoder.Create(UnicodeRanges.All)
            };
            if (m_voiceManager == null)
                return JsonSerializer.Serialize(new { code = ResultCode.NotFound, list = renderList }, options);


            int code = ResultCode.NotImplemented;
            try
            {
                renderList = m_voiceManager.GetRenderList();
                if (renderList.Count > 0)
                {
                    code = ResultCode.Success;
                }
                else
                {
                    code = ResultCode.DeviceNotReady;
                }
            }
            catch (Exception)
            {
                code = ResultCode.InternalError;
            }

            return JsonSerializer.Serialize(new { code = code, list = renderList }, options);
        }

        /// <summary>
        /// 입력장치 리스트 반환
        /// </summary>
        /// <returns>
        /// {"code": "200", "list": ["mic1", "mic2"]}
        /// </returns>
        private string GetCaptureList(JsonElement param)
        {
            List<string>? captureList = null;
            var options = new JsonSerializerOptions
            {
                Encoder = JavaScriptEncoder.Create(UnicodeRanges.All)
            };
            if (m_voiceManager == null)
                return JsonSerializer.Serialize(new { code = ResultCode.NotFound, list = captureList }, options);


            int code = ResultCode.NotImplemented;
            try
            {
                captureList = m_voiceManager.GetCaptureList();
                if (captureList.Count > 0)
                {
                    code = ResultCode.Success;
                }
                else
                {
                    code = ResultCode.DeviceNotReady;
                }
            }
            catch (Exception)
            {
                code = ResultCode.InternalError;
            }

            return JsonSerializer.Serialize(new { code = code, list = captureList }, options);
        }

        /// <summary>
        /// 출력장치 리스트(장치명) 반환
        /// </summary>
        /// <returns>
        /// {"code": "200", "list": ["spk1", "spk2"]}
        /// </returns>
        private string GetRenderName(JsonElement param)
        {
            string renderName = "";
            if (m_voiceManager == null) return $"{{\"code\":{ResultCode.NotFound},\"name\":\"{renderName}\"}}";


            int code = ResultCode.NotImplemented;
            try
            {
                renderName = m_voiceManager.GetRenderName();
                if (string.IsNullOrEmpty(renderName))
                {
                    code = ResultCode.ServiceUnavailable;
                }
                else
                {
                    code = ResultCode.Success;
                }
            }
            catch (Exception)
            {
                code = ResultCode.InternalError;
            }

            return $"{{\"code\":{code},\"name\":\"{renderName}\"}}";
        }

        /// <summary>
        /// 입력장치 리스트(장치명) 반환
        /// </summary>
        /// <returns>
        /// {"code": "200", "list": "['mic1', 'mic2']"}
        /// </returns>
        private string GetCaptureName(JsonElement param)
        {
            string captureName = "";
            if (m_voiceManager == null) return $"{{\"code\":{ResultCode.NotFound},\"name\":\"{captureName}\"}}";


            int code = ResultCode.NotImplemented;
            try
            {
                captureName = m_voiceManager.GetCaptureName();
                if (string.IsNullOrEmpty(captureName))
                {
                    code = ResultCode.ServiceUnavailable;
                }
                else
                {
                    code = ResultCode.Success;
                }
            }
            catch (Exception)
            {
                code = ResultCode.InternalError;
            }

            return $"{{\"code\":{code},\"name\":\"{captureName}\"}}";
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
        private string GetStringFromJson(JsonElement obj, string propName)
        {
            return obj.TryGetProperty(propName, out var elem) && elem.ValueKind == JsonValueKind.String
                ? elem.GetString() ?? ""
                : "";
        }


    }

}