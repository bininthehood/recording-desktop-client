using RecordClient.Helpers;
using RecordClient.Helpers.InterfaceSocket;
using RecordClient.ViewModels;
using System.IO;
using static RecordClient.Helpers.InterfaceSocket.InterfaceSocket;


namespace RecordClient.Services.Record
{
    public class VoiceManager
    {
        private static readonly object _recordLock = new();
        internal class RecData
        {
            private volatile bool _isProcessing = false;

            public string? Mode { get; set; }
            public List<string>? Aic { get; set; }
            public string? Reckey { get; set; }
            public string? Subkey { get; set; }
            public string? UserId { get; set; }
            public string? Mix { get; set; }
            public string? Headset { get; set; }
            public string? Opt { get; set; }
            public int? Vol { get; set; }
            public string? Filename { get; set; }
            public string? Filepath { get; set; }
            public int Fileformat { get; set; }
            public string? ServerIp_1 { get; set; }
            public int ServerPort_1 { get; set; }
            public string? ServerIp_2 { get; set; }
            public int ServerPort_2 { get; set; }
            public string? Tls { get; set; }
            public int? TestConn { get; set; }
            
            public void SafeReset()
            {
                if (_isProcessing)
                {
                    Logger.Error("녹취 데이터 초기화 생략");
                    return;
                }

                try
                {
                    _isProcessing = true;

                    Mode = null;
                    Aic = null;
                    Reckey = null;
                    Subkey = null;
                    Mix = null;
                    Headset = null;
                    Opt = null;
                    Vol = null;
                    Filename = null;
                    Filepath = null;
                    Fileformat = 0;
                    ServerIp_1 = null;
                    ServerPort_1 = 0;
                    ServerIp_2 = null;
                    ServerPort_2 = 0;
                    Tls = null;
                    TestConn = null;

                    Logger.Info("녹취 데이터 초기화 완료");
                }
                finally
                {
                    _isProcessing = false;
                }
            }
        }

        private readonly RecData _recData = new RecData();

        private static readonly string TAG = typeof(VoiceManager).Name;
        private static VoiceManager? m_voiceManager = null;
        private static AudioManager? m_audioManager = null;
        private static AudioPlayer? m_audioPlayer = null;
        public bool m_isAudioOpen = false;
        public int m_status = 0;


        private DateTime m_startDate = DateTime.Now;
        private DateTime? m_endDate = null;

        public static VoiceManager GetInstance()
        {
            if (m_voiceManager == null)
            {
                m_voiceManager = new VoiceManager();
                m_audioPlayer = new AudioPlayer();

                InterfaceSocket.Init(m_voiceManager, m_audioPlayer);
                InterfaceSocket.Instance.Start();
                InterfaceSocketLevel.Init(m_voiceManager);
                InterfaceSocketLevel.Instance.Start();
            }

            return m_voiceManager;
        }


        public int IsWorking() => m_status;

        public String GetUserId()
        {
            if (_recData != null && _recData.UserId != null)
            {
                return _recData.UserId;
            }
            else
            {
                return "";
            }
        }

        public string GetRecordStatus()
        {
            if (m_status == 1)
                return "recording";
            else
                return "stop";
        }

        public DateTime? GetEndDate() => m_endDate;
                

        // 필수 데이터 초기화
        public void SetRequiredData(string reckey, string subkey, string filePath, string fileName, int format)
        {
            lock (_recordLock)
            {
                _recData.Reckey = reckey;
                _recData.Subkey = subkey;
                _recData.Filepath = filePath;
                _recData.Filename = fileName;
                _recData.Fileformat = format;
            }
        }

        // 보조 데이터 초기화
        public void SetOptionalData(string mode, string userId, string mix, string headset, string opt, string tls, int testConn, List<string> aic)
        {
            lock (_recordLock)
            {
                _recData.Mode = mode;
                _recData.UserId = userId;

                _recData.Mix = mix;
                _recData.Headset = headset;
                _recData.Opt = opt;
                _recData.Aic = aic;

                _recData.Tls = tls;
                _recData.TestConn = testConn;
            }
        }

        public bool StartRecording()
        {
            lock (_recordLock)
            {
                if (m_status == 1) // 프로세스 진행중인 경우
                {
                    Logger.Error($"[{TAG}] 기존 녹음 중단 후 재시작");

                    StopRecording(); // 자원 정리
                    Thread.Sleep(100); // 약간의 대기 (파일 닫힘 등 반영 대기)
                }

                m_startDate = DateTime.Now;
                m_endDate = null;

                if (!m_isAudioOpen)
                {
                    // 오디오 매니저 초기화
                    m_audioManager = AudioOpen(_recData.Fileformat);

                    if (m_audioManager == null)
                    {
                        Logger.Error($"[{TAG}] 오디오 매니저 열기 실패");
                        return false;
                    }
                    else
                    {
                        bool startOK = m_audioManager.Start();

                        if (!startOK) // 녹취 시작
                        {
                            Logger.Error($"[{TAG}] 오디오 관리자에서 녹취 시작을 할 수 없습니다.");
                            return false;
                        }
                    }
                }
                else
                {
                    Logger.Error($"[{TAG}] 오디오 프로세스가 이미 열린 상태입니다.");

                    if (m_audioManager == null)
                    {
                        Logger.Error($"[{TAG}] 오디오 프로세스 열기 실패");
                        return false;
                    }
                    else
                    {
                        bool startOK = m_audioManager.Start();

                        if (!startOK) // 녹취 시작
                        {
                            Logger.Error($"[{TAG}] 오디오 관리자에서 녹취 시작을 할 수 없습니다.");
                            return false;
                        }
                    }
                }

                Logger.Info($"[{TAG}] 오디오 프로세스 열기 성공 . ");
                m_status = 1;
                return true;
            }
        }

        public async Task<bool> StartRecordingAsync()
        {
            return await Task.Run(() =>
            {
                lock (_recordLock)
                {
                    if (m_status == 1)
                    {
                        Logger.Error($"[{TAG}] 기존 녹음 중단 후 재시작");
                        StopRecording();
                        Thread.Sleep(100); // ← 이 부분도 Task.Delay로 대체 가능
                    }

                    m_startDate = DateTime.Now;
                    m_endDate = null;

                    if (!m_isAudioOpen)
                    {
                        m_audioManager = AudioOpen(_recData.Fileformat);
                        if (m_audioManager == null)
                        {
                            Logger.Error($"[{TAG}] 오디오 매니저 열기 실패");
                            return false;
                        }

                        bool startOK = m_audioManager.Start();
                        if (!startOK)
                        {
                            Logger.Error($"[{TAG}] 오디오 관리자에서 녹취 시작 실패");
                            return false;
                        }
                    }
                    else
                    {
                        if (m_audioManager == null)
                        {
                            Logger.Error($"[{TAG}] 오디오 매니저 없음");
                            return false;
                        }

                        bool startOK = m_audioManager.Start();
                        if (!startOK)
                        {
                            Logger.Error($"[{TAG}] 오디오 관리자에서 녹취 시작 실패");
                            return false;
                        }
                    }

                    Logger.Info($"[{TAG}] 오디오 프로세스 열기 성공");
                    m_status = 1;
                    return true;
                }
            });
        }




        public bool PauseRecording()
        {
            if (m_audioManager == null) return false;

            return m_audioManager.Pause();
        }
        public bool StopRecording()
        {
            lock (_recordLock)
            {
                Logger.Info($"[{TAG}] m_status - " + m_status);

                if (m_status != 1) return false;

                m_status = 0;
                m_endDate = DateTime.Now;

                if (m_audioManager == null) return false;

                AudioClose();

                return true;
            }
        }
        public void ResetRecData()
        {
            lock (_recordLock)
            {
                _recData.SafeReset();
            }
        }
        /*
        * @Description 녹취 종료 이벤트 호출
        */
        public bool AudioClose()
        {
            lock (_recordLock)
            {
                if (m_audioManager != null)
                {
                    m_audioManager.Stop();
                    m_audioManager.SetOutputFile("", "");

                    m_isAudioOpen = false;
                    return true;
                }

                return false;
            }
        }


        /// <summary>
        /// 녹취 시작 이벤트 호출
        /// </summary>
        /// <param type="int" name="fileFormat">
        /// 파일 포맷 ( 1: WAV/GSM, 2: MP3 )
        /// </param>
        /// <returns></returns>
        public AudioManager? AudioOpen(int fileFormat)
        {
            lock (_recordLock)
            {
                if (fileFormat < 1) fileFormat = 1;

                try
                {
                    if (string.IsNullOrWhiteSpace(_recData.ServerIp_1) ||
                        _recData.ServerPort_1 <= 0 ||
                        string.IsNullOrWhiteSpace(_recData.Reckey) ||
                        string.IsNullOrWhiteSpace(_recData.Subkey) )
                    {
                        Logger.Error($"[{TAG}] 누락 된 파라미터 존재");
                        Logger.Error($"[{TAG}] Server IP   - {_recData.ServerIp_1}");
                        Logger.Error($"[{TAG}] Server Port - {_recData.ServerPort_1}");
                        Logger.Error($"[{TAG}] Record Key  - {_recData.Reckey}");
                        Logger.Error($"[{TAG}] Sub Key     - {_recData.Subkey}");

                        return null;
                    }

                    AudioManager manager = new AudioManager();

                    manager.SetEngineUrl(1, _recData.ServerIp_1, _recData.ServerPort_1);
                    manager.SetEngineUrl(2, _recData.ServerIp_2, _recData.ServerPort_2);
                    manager.SetRecordKey(_recData.Reckey, _recData.Subkey);
                    manager.SetOutputFile(_recData.Filepath, _recData.Filename);

                    m_isAudioOpen = true;

                    bool initiatingOK = manager.initRecorder(fileFormat); // 오디오 객체 초기화
                    if (initiatingOK)
                    {
                        return manager;
                    }
                    else
                    {
                        Logger.Error($"[{TAG}] 오디오 관리자 초기화 실패: 녹음 준비 안됨.");

                        return null;
                    }

                }
                catch (IOException ex)
                {
                    Logger.Error($"[{TAG}] IO 오류: {ex.Message}");
                }
                catch (ArgumentException ex)
                {
                    Logger.Error($"[{TAG}] 인자 오류: {ex.Message}");
                }
                catch (Exception ex)
                {
                    Logger.Error($"[{TAG}] 일반 오류: {ex.Message}");
                }

                return null;
            }
        }

        //  사용안함 
        public float GetLevel()
        {
            return UIHelper.CallVm<MainPageViewModel, float>(vm =>
            {
                return vm.InputVolumeLevel;
            }, ResultCode.InternalError);
        }

        public string GetInputDeviceStatus()
        {
            string result = "";
            try
            {
                if (DeviceService.Instance.IsInputDeviceAvailable())
                {
                    result = "open";
                    if (m_audioManager != null && m_audioManager.GetPaused())
                    {
                        result = "pause";
                    }
                }
                else
                {
                    result = "stop";
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"[GetRenderName 예외] {ex}");

            }

            return result;
        }
        public int GetRenderMute()
        {
            try
            {
                return DeviceService.Instance.GetOutputMute() ? 0 : 1;
            }
            catch (Exception ex)
            {
                Logger.Error($"[GetRenderList 예외] {ex}");
            }

            return -1;
        }

        public int GetCaptureMute()
        {
            try
            {
                return DeviceService.Instance.GetInputMute() ? 0 : 1;
            }
            catch (Exception ex)
            {
                Logger.Error($"[GetRenderList 예외] {ex}");
            }

            return -1;
        }
        public List<string> GetRenderList()
        {
            try
            {
                return DeviceService.Instance.GetOutputDeviceListString();
            }
            catch (Exception ex)
            {
                Logger.Error($"[GetRenderList 예외] {ex}");
            }

            return new List<string>();
        }
        public List<string> GetCaptureList()
        {
            try
            {
                return DeviceService.Instance.GetInputDeviceListString();
            }
            catch (Exception ex)
            {
                Logger.Error($"[GetRenderList 예외] {ex}");
            }

            return new List<string>();
        }

        public string GetRenderName()
        {
            try
            {
                return DeviceService.Instance.GetDefaultOutputDeviceName();
            }
            catch (Exception ex)
            {
                Logger.Error($"[GetCaptureName 예외] {ex}");
            }

            return string.Empty;
        }


        public string GetCaptureName()
        {
            try
            {

                return DeviceService.Instance.GetDefaultInputDeviceName();
            }
            catch (Exception ex)
            {
                Logger.Error($"[GetCaptureName 예외] {ex}");
            }

            return string.Empty;
        }

        public bool SetCaptureMute(int mute)
        {
            return UIHelper.CallVm<MainPageViewModel, bool>(vm =>
            {
                bool isMute = mute > 0 ? true : false;
                vm.IsInputMuted = isMute;

                return true;
            }, false);
        }

        public bool SetRenderMute(int mute)
        {
            return UIHelper.CallVm<MainPageViewModel, bool>(vm =>
            {
                bool isMute = mute > 0 ? true : false;
                vm.IsOutputMuted = isMute;

                return true;
            }, false);
        }

        public int SetCaptureName(string deviceName)
        {
            return UIHelper.CallVm<MainPageViewModel, int>(vm =>
            {
                var matchedDevice = vm.InputDeviceList?
                    .FirstOrDefault(d => d.Name == deviceName);

                if (matchedDevice != null)
                {
                    vm.SelectedInputDevice = matchedDevice;
                    return ResultCode.Success;
                }

                Logger.Warn($"입력 장치 '{deviceName}' 을 찾을 수 없습니다.");
                return ResultCode.NotFound;
            }, ResultCode.InternalError);
        }

        public int SetRenderName(string deviceName)
        {
            return UIHelper.CallVm<MainPageViewModel, int>(vm =>
            {
                var matchedDevice = vm.OutputDeviceList?
                    .FirstOrDefault(d => d.Name == deviceName);

                if (matchedDevice != null)
                {
                    vm.SelectedOutputDevice = matchedDevice;
                    return ResultCode.Success;
                }

                Logger.Warn($"출력 장치 '{deviceName}' 을 찾을 수 없습니다.");
                return ResultCode.NotFound;
            }, ResultCode.InternalError);
        }

        public int SetRenderVol(int volume)
        {
            return UIHelper.CallVm<MainPageViewModel, int>(vm =>
            {
                if (volume < 0 || volume > 100)
                    return ResultCode.VolumeOutOfRange;

                float v = volume / 100f;

                vm.OutputVolume = v;
                return ResultCode.Success;
            }, ResultCode.InternalError);
        }

        public int SetCaptureVol(int volume)
        {
            return UIHelper.CallVm<MainPageViewModel, int>(vm =>
            {
                if (volume < 0 || volume > 100)
                    return ResultCode.VolumeOutOfRange;

                float v = volume / 100f;

                vm.InputVolume = v;
                return ResultCode.Success;
            }, ResultCode.InternalError);
        }

        public int GetRenderVol()
        {
            try
            {
                return (int) DeviceService.Instance.GetOutputVolume();
            }
            catch (Exception ex)
            {
                Logger.Error($"[GetCaptureName 예외] {ex}");
            }

            return -1;
        }

        public int GetCaptureVol()
        {
            try
            {
                return (int) DeviceService.Instance.GetInputVolume();
            }
            catch (Exception ex)
            {
                Logger.Error($"[GetCaptureName 예외] {ex}");
            }

            return -1;
        }

        public void SetEngineUrl(int no, string url, int port)
        {
            if (!System.Net.IPAddress.TryParse(url, out _))
            {
                Logger.Error($"[{TAG}] 잘못된 URL 형식: {url}");
                return;
            }

            if (port < 1 || port > 65535)
            {
                Logger.Error($"[{TAG}] 잘못된 포트 번호: {port}");
                return;
            }

            switch (no)
            {
                case 1:
                    _recData.ServerIp_1 = url;
                    _recData.ServerPort_1 = port;
                    break;
                case 2:
                    _recData.ServerIp_2 = url;
                    _recData.ServerPort_2 = port;
                    break;
            }
        }
        public string? GetRecKey()
        {
            string? result = null;
            if (!string.IsNullOrEmpty(_recData.Reckey))
                result = _recData.Reckey;
            return result;
        }
        public string? GetSubKey()
        {
            string? result = null;
            if (!string.IsNullOrEmpty(_recData.Subkey))
                result = _recData.Subkey;
            return result;
        }
        public string GetHost()
        {
            string result = "";
            if (!string.IsNullOrEmpty(_recData.ServerIp_1)) result = _recData.ServerIp_1;
            return result;
        }
        public int GetPort()
        {
            int result = 0;
            if (_recData.ServerPort_1 != 0) result = _recData.ServerPort_1;
            return result;
        }

        public void PushInfo(string serverInfo, string _type, string _data)
        {

            try
            {
                Instance.Broadcast(
                    model: "00",
                    type: "push",
                    command: "info",
                    param: new { code = 200, server = serverInfo, type = _type, data = _data }
                );
            }
            catch (Exception)
            {
            }
        }

        public int PushStatus(int result, string aud, string stat)
        {

            try
            {
                Instance.Broadcast(
                    model: "00",
                    type: "push",
                    command: "status",
                    param: new { code = result, audio = aud, status = stat }
                );
            }
            catch (Exception)
            {
                result = ResultCode.PartialSuccess;
            }

            return result;
        }

        public int PushAudStatus(int result, string str)
        {
            try
            {
                Instance.Broadcast(
                    model: "00",
                    type: "push",
                    command: "status",
                    param: new { code = result, audio = str }
                );
            }
            catch (Exception)
            {
                result = ResultCode.PartialSuccess;
            }

            return result;
        }
        public int PushCaptureVol(int result, int volume)
        {
            try
            {
                Instance.Broadcast(
                    model: "00",
                    type: "push",
                    command: "status",
                    param: new { code = result, vol = volume }
                );
            }
            catch (Exception)
            {
                result = ResultCode.PartialSuccess;
            }

            return result;
        }
        public int PushRenderVol(int result, int volume)
        {
            try
            {
                Instance.Broadcast(
                    model: "00",
                    type: "push",
                    command: "status",
                    param: new { code = result, vol = volume }
                );
            }
            catch (Exception)
            {
                result = ResultCode.PartialSuccess;
            }

            return result;
        }
        public int PushRenderMute(int result, bool mute)
        {
            int muteNum = mute ? 0 : 1;
            try
            {
                Instance.Broadcast(
                    model: "00",
                    type: "push",
                    command: "status",
                    param: new { code = result, mute = muteNum }
                );
            }
            catch (Exception)
            {
                result = ResultCode.PartialSuccess;
            }

            return result;
        }

        public int PushCaptureMute(int result, bool mute)
        {
            int muteNum = mute ? 0 : 1;
            try
            {
                Instance.Broadcast(
                    model: "00",
                    type: "push",
                    command: "status",
                    param: new { code = result, mute = muteNum }
                );
            }
            catch (Exception)
            {
                result = ResultCode.PartialSuccess;
            }

            return result;
        }






        public string CheckRecDataFolder(string basePath)
        {
            DateTime startDate = DateTime.Now;
            string nowDt = startDate.ToString("yyyyMMdd");

            // 기본 경로 폴더 생성
            if (!Directory.Exists(basePath))
            {
                Directory.CreateDirectory(basePath);
            }

            // 날짜별 하위 폴더 생성
            string dateFolderPath = Path.Combine(basePath, nowDt);
            if (!Directory.Exists(dateFolderPath))
            {
                Directory.CreateDirectory(dateFolderPath);
            }

            // 이전 날짜 폴더 정리
            var baseDir = new DirectoryInfo(basePath);
            foreach (var dir in baseDir.GetDirectories())
            {
                if (!dir.Name.Equals(nowDt))
                {
                    Logger.Info($"[CheckRecDataFolder] Remove Old Folder - {dir.Name}");
                    try
                    {
                        dir.Delete(true); // 하위 파일 포함 삭제
                    }
                    catch (Exception ex)
                    {
                        Logger.Error($"[CheckRecDataFolder] 삭제 실패: {ex.Message}");
                    }
                }
            }

            return dateFolderPath;
        }


        public long GetDateDuration(DateTime? startDate, DateTime? endDate)
        {
            if (endDate.HasValue)
            {
                if (!startDate.HasValue) return 0L;

                long dur = (long)(endDate.Value - startDate.Value).TotalMilliseconds;
                if (dur < 0L) dur = 0L;
                return dur;
            }
            return 0L;
        }
    }
}