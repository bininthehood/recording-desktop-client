using NAudio.CoreAudioApi;
using NAudio.Wave;
using RecordClient.Helpers;
using RecordClient.Helpers.Config;
using RecordClient.Helpers.Popup;
using RecordClient.Models.Comment;
using RecordClient.Models.Device;
using RecordClient.Models.Login;
using RecordClient.Services;
using RecordClient.Services.Device;
using RecordClient.Services.Login;
using RecordClient.Services.Record;
using RecordClient.Services.Sending;
using RecordClient.Services.Utils;
using RecordClient.Views;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;

namespace RecordClient.ViewModels
{
    public class MainPageViewModel : INotifyPropertyChanged, IDisposable
    {
        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));


        // ──────────────── 커맨드 정의 ────────────────
        public ICommand? ToggleOutputMuteCommand { get; set; }   // 출력 음소거 버튼
        public ICommand? ToggleInputMuteCommand { get; set; }    // 입력 음소거 버튼
        public ICommand? ToggleTabCommand { get; }               // 보조 영역 버튼
        public ICommand? ToggleRecordCommand { get; set; }       // 녹음 버튼
        public ICommand? StartCommand { get; set; }                  // 녹취 시작 버튼
        public ICommand? StopCommand { get; set; }                   // 녹취 종료 버튼
        public ICommand? ToggleInputMonitoring { get; set; }        // 마이크 모니터링 버튼

        public bool IsAdjustmentVisible => SelectedTabIndex == 0 && IsSubAreaVisible;   // 장치 조절 열기
        public bool IsCommentVisible => SelectedTabIndex == 1 && IsSubAreaVisible;      // 코멘트 열기


        // ──────────────── 서비스 및 내부 객체 ────────────────
        private DeviceWatcher? _deviceWatcher;
        public VoiceManager _voiceManager;

        // ──────────────── 녹음  ────────────────
        private bool _isRecording = false;
        private DispatcherTimer? _timer;
        private TimeSpan _elapsedTime;
        private string _timerText = "00:00:00";

        // ──────────────── 장치  ────────────────
        private ObservableCollection<DeviceItem>? _inputDeviceList;
        private ObservableCollection<DeviceItem>? _outputDeviceList;
        private DeviceItem? _selectedInputDevice;
        private DeviceItem? _selectedOutputDevice;
        private bool _isInputMuted;
        private bool _isOutputMuted;

        private float _inputVolume;
        private float _outputVolume;

        private CancellationTokenSource? _reloadToken; // 장치 전환 오류 방지용 토큰
        // ──────────────── 장치 모니터링  ────────────────

        private WasapiCapture? _inputCapture = null;
        private WasapiLoopbackCapture? _outputCapture = null;

        private float _inputVolumeLevel;
        private float _outputVolumeLevel;
        private double _smoothedInputVolumeLevel;
        private double _smoothedOutputVolumeLevel;

        private bool _isInputMonitoringOn;
        private const float VolumeSmoothingFactor = 0.2f; // 0.0 ~ 1.0, 작을수록 느리게 변화


        // ──────────────── 하단 영역 제어 ────────────────
        private bool _isSubAreaVisible;
        private int _selectedTabIndex;
        private bool _playOpenAnimation;
        private bool _playCloseAnimation;


        // ──────────────── 코멘트 ────────────────
        private CommentWorker? commentWorker;
        public ObservableCollection<Comment>? _commentMessages;     // 코멘트 목록
        private string _inputCommentText = "";



        // ──────────────────────────────────────────────────────────────────────────────────────────
        //                                   ViewModel 초기화 영역
        // ──────────────────────────────────────────────────────────────────────────────────────────
        public MainPageViewModel()
        {
            // 녹음 및 기타
            _voiceManager = VoiceManager.GetInstance(); // 녹취 객체
            _deviceWatcher = new DeviceWatcher(); // 장치 연결/해제 감지하는 객체
            // 타이머
            _timer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(1)
            };
            _timer.Tick += (s, e) =>
            {
                _elapsedTime = _elapsedTime.Add(TimeSpan.FromSeconds(1));
                TimerText = _elapsedTime.ToString(@"hh\:mm\:ss");
            };

            // 보조 영역 제어
            ToggleTabCommand = new RelayCommand<int>(ToggleTab);

            CommentMessages = new ObservableCollection<Comment>(); // 코멘트 객체
        }

        public async Task InitializeAsync()
        {
            // 초기 값 로딩
            InitializeCommands();
            InitializeProperties();

            await Task.Delay(1); // UI 프레임 렌더링 기회 제공 (프리징 완화)

            // 장치 탐색 등 
            LoadDeviceList();

            // 이벤트 구독, 상태 모니터링 시작
            StartMonitoring();
        }
        // ──────────────── [ 커맨드 초기화 ] ────────────────
        private void InitializeCommands()
        {


            // 음소거 제어
            ToggleInputMuteCommand = new RelayCommand(() =>
            {
                IsInputMuted = !IsInputMuted;
            });
            ToggleOutputMuteCommand = new RelayCommand(() =>
            {
                IsOutputMuted = !IsOutputMuted;
            });

            // 녹취 제어
            StartCommand = new RelayCommand(StartRecording);
            StopCommand = new RelayCommand(StopRecording);
            ToggleRecordCommand = new RelayCommand(() =>
            {
                if (IsRecording)
                {
                    StopCommand.Execute(null);
                }
                else
                {
                    StartCommand.Execute(null);
                }
            });

            /*ToggleInputMonitoring = new RelayCommand(() =>
            {
                IsInputMonitoringOn = !IsInputMonitoringOn;
            });*/


        }

        // ──────────────── [ 변수 초기화 ] ────────────────
        private void InitializeProperties()
        {
            TimerText = "00:00:00";
            SmoothedInputVolumeLevel = 0;
            SmoothedOutputVolumeLevel = 0;
            IsSubAreaVisible = false;
            SelectedTabIndex = 0;
            PlayOpenAnimation = false;
            PlayCloseAnimation = false;
        }


        // ──────────────── [ 장치 / 서비스 초기화 ] ────────────────
        private void LoadDeviceList()
        {

            // 입력 장치 초기화
            var inputList = LoadInputDevices();
            InputDeviceList = new ObservableCollection<DeviceItem>(inputList);

            var defaultInputId = DeviceService.Instance.GetDefaultInputDeviceID();
            SelectedInputDevice = inputList.FirstOrDefault(d => d.ID == defaultInputId) ?? inputList.FirstOrDefault();

            // 출력 장치 초기화
            var outputList = LoadOutputDevices();
            OutputDeviceList = new ObservableCollection<DeviceItem>(outputList);

            var defaultOutputId = DeviceService.Instance.GetDefaultOutputDeviceID();
            SelectedOutputDevice = outputList.FirstOrDefault(d => d.ID == defaultOutputId) ?? outputList.FirstOrDefault();

        }

        // ──────────────── [ 이벤트 초기화 ] ────────────────
        private void StartMonitoring()
        {
            if (_deviceWatcher != null)
            {
                _deviceWatcher.DeviceChanged += async (s, e) =>
                {
                    if (e.Reason is "Added" or "Removed" or "State")
                    {
                        _reloadToken?.Cancel(); // 이전 호출 무효화
                        _reloadToken = new CancellationTokenSource();

                        try
                        {
                            // 장치 등록 처리 대기 (약간의 시간 지연)
                            await Task.Delay(500, _reloadToken.Token);
                        }
                        catch (TaskCanceledException)
                        {
                            return;
                        }

                        if (IsRecording)
                        {
                            Logger.Debug("녹취 중 .. 현재 녹취 종료");
                            StopCommand?.Execute(null);
                        }

                        var prevInputIds = InputDeviceList?.Select(d => d.ID).ToList();
                        InputDeviceList = new ObservableCollection<DeviceItem>(await WaitForStableInputDevices(prevInputIds));

                        var prevOutputIds = OutputDeviceList?.Select(d => d.ID).ToList();
                        OutputDeviceList = new ObservableCollection<DeviceItem>(await WaitForStableOutputDevices(prevOutputIds));

                        if (InputDeviceList.Any())
                        {
                            Logger.Debug("입력 장치 변경 감지 → 목록 최신화 완료");
                            SelectedInputDevice = InputDeviceList.First();
                        }
                        if (OutputDeviceList.Any())
                        {
                            Logger.Debug("출력 장치 변경 감지 → 목록 최신화 완료");
                            SelectedOutputDevice = OutputDeviceList.First();
                        }

                    }
                };
            }
        }

        // ──────────────── [ 자원 해제 ] ────────────────
        public void Dispose()
        {
            _inputCapture?.StopRecording();
            _inputCapture?.Dispose();
            _inputCapture = null;

            _outputCapture?.StopRecording();
            _outputCapture?.Dispose();
            _outputCapture = null;

            _timer?.Stop();
            _timer = null;

            _deviceWatcher?.Dispose();
            _deviceWatcher = null;


            commentWorker = null;
        }





        // ──────────────────────────────────────────────────────────────────────────────────────────
        //                                     UI 속성 정의
        // ──────────────────────────────────────────────────────────────────────────────────────────
        public string RecordIconPath =>
            IsRecording
            ? "pack://application:,,,/Resources/Images/square_white.png"
            : "pack://application:,,,/Resources/Images/record_white.png";
        public string MicIconPath =>
            IsInputMuted
            ? "pack://application:,,,/Resources/Images/mic_red.png"
            : "pack://application:,,,/Resources/Images/mic_white.png";

        public string SpeakerIconPath =>
            IsOutputMuted
            ? "pack://application:,,,/Resources/Images/headphone_red.png"
            : "pack://application:,,,/Resources/Images/headphone_white.png";

        public string SettingIconPath =>
            SelectedTabIndex == 0 && IsSubAreaVisible
                ? "pack://application:,,,/Resources/Images/setting_red.png"
                : "pack://application:,,,/Resources/Images/setting_white.png";

        public string ChatIconPath =>
            SelectedTabIndex == 1 && IsSubAreaVisible
                ? "pack://application:,,,/Resources/Images/chat_red.png"
                : "pack://application:,,,/Resources/Images/chat_white.png";

        public bool IsRecording
        {
            get => _isRecording;
            set
            {
                _isRecording = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(RecordIconPath));
            }
        }
        public bool IsSubAreaVisible
        {
            get => _isSubAreaVisible;
            set
            {
                if (_isSubAreaVisible != value)
                {
                    _isSubAreaVisible = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(SettingIconPath));
                    OnPropertyChanged(nameof(ChatIconPath));
                    OnPropertyChanged(nameof(IsCommentVisible));
                    OnPropertyChanged(nameof(IsAdjustmentVisible));
                }
            }
        }

        public int SelectedTabIndex
        {
            get => _selectedTabIndex;
            set
            {
                if (_selectedTabIndex != value)
                {
                    _selectedTabIndex = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(SettingIconPath));
                    OnPropertyChanged(nameof(ChatIconPath));
                    OnPropertyChanged(nameof(IsCommentVisible));
                    OnPropertyChanged(nameof(IsAdjustmentVisible));
                }
            }
        }

        public ObservableCollection<DeviceItem>? InputDeviceList
        {
            get => _inputDeviceList;
            set
            {
                _inputDeviceList = value;
                OnPropertyChanged(nameof(InputDeviceList));
            }
        }
        public ObservableCollection<DeviceItem>? OutputDeviceList
        {
            get => _outputDeviceList;
            set
            {
                _outputDeviceList = value;
                OnPropertyChanged(nameof(OutputDeviceList));
            }
        }

        public DeviceItem? SelectedInputDevice
        {
            get => _selectedInputDevice;
            set
            {
                if (_selectedInputDevice != value)
                {
                    _selectedInputDevice = value;
                    OnPropertyChanged();
                }
            }
        }

        public DeviceItem? SelectedOutputDevice
        {
            get => _selectedOutputDevice;
            set
            {
                if (_selectedOutputDevice != value)
                {
                    _selectedOutputDevice = value;
                    OnPropertyChanged();
                }
            }
        }

        public float InputVolume
        {
            get => _inputVolume;
            set
            {
                if (_inputVolume != value)
                {
                    _inputVolume = value;
                    OnPropertyChanged();

                }
            }
        }
        public float OutputVolume
        {
            get => _outputVolume;
            set
            {
                if (_outputVolume != value)
                {
                    _outputVolume = value;
                    OnPropertyChanged();
                }
            }
        }
        public float InputVolumeLevel
        {
            get => _inputVolumeLevel;
            set
            {
                _inputVolumeLevel = value;
                OnPropertyChanged();
            }
        }
        public float OutputVolumeLevel
        {
            get => _outputVolumeLevel;
            set
            {
                _outputVolumeLevel = value;
            }
        }
        public double SmoothedInputVolumeLevel
        {
            get => _smoothedInputVolumeLevel;
            set
            {
                if (Math.Abs(_smoothedInputVolumeLevel - value) > 0.0001)
                {
                    _smoothedInputVolumeLevel = value;
                    OnPropertyChanged(nameof(SmoothedInputVolumeLevel));
                }
            }

        }
        public double SmoothedOutputVolumeLevel
        {
            get => _smoothedOutputVolumeLevel;
            set
            {
                if (_smoothedOutputVolumeLevel != value)
                {
                    _smoothedOutputVolumeLevel = value;
                    OnPropertyChanged(nameof(SmoothedOutputVolumeLevel));
                }
            }
        }

        public bool IsOutputMuted
        {
            get => _isOutputMuted;
            set
            {
                if (_isOutputMuted != value)
                {
                    _isOutputMuted = value;
                    OnPropertyChanged(nameof(IsOutputMuted));
                    OnPropertyChanged(nameof(SpeakerIconPath));
                    DeviceService.Instance.CommitOutputMute(value);
                }
            }
        }


        public bool IsInputMuted
        {
            get => _isInputMuted;
            set
            {
                if (_isInputMuted != value)
                {
                    _isInputMuted = value;
                    OnPropertyChanged(nameof(IsInputMuted));
                    OnPropertyChanged(nameof(MicIconPath));
                    DeviceService.Instance.CommitInputMute(value);
                }
            }
        }

        /*public bool IsInputMonitoringOn
        {
            get => _isInputMonitoringOn;
            set
            {
                if (_isInputMonitoringOn != value)
                {
                    _isInputMonitoringOn = value;
                    OnPropertyChanged(nameof(IsInputMonitoringOn));
                    InputMonotoringOn(value);
                }
            }
        }*/

        public string TimerText
        {
            get => _timerText;
            set { _timerText = value; OnPropertyChanged(); }
        }


        public string InputCommentText
        {
            get => _inputCommentText;
            set
            {
                if (_inputCommentText != value)
                {
                    _inputCommentText = value;
                    OnPropertyChanged();
                }
            }
        }
        public bool PlayOpenAnimation
        {
            get => _playOpenAnimation;
            set
            {
                if (_playOpenAnimation != value)
                {
                    _playOpenAnimation = value;
                    OnPropertyChanged();
                }
            }
        }

        public bool PlayCloseAnimation
        {
            get => _playCloseAnimation;
            set
            {
                if (_playCloseAnimation != value)
                {
                    _playCloseAnimation = value;
                    OnPropertyChanged();
                }
            }
        }
        public ObservableCollection<Comment>? CommentMessages
        {
            get => _commentMessages;
            set
            {
                if (_commentMessages != value)
                {
                    _commentMessages = value;
                    OnPropertyChanged();
                }
            }
        }

        // ──────────────────────────────────────────────────────────────────────────────────────────
        //                                      메서드 정의
        // ──────────────────────────────────────────────────────────────────────────────────────────


        // ──────────────── [장치 목록 로드] ────────────────

        private List<DeviceItem> LoadInputDevices()
        {
            return DeviceService.Instance.GetInputDevices();
        }
        private List<DeviceItem> LoadOutputDevices()
        {
            return DeviceService.Instance.GetOutputDevices();
        }
        private async Task<List<DeviceItem>> WaitForStableInputDevices(List<string> previousIds, int retry = 5)
        {
            for (int i = 0; i < retry; i++)
            {
                var list = LoadInputDevices();
                var newIds = list.Select(d => d.ID).ToList();

                // 새로운 장치 ID가 존재하는 경우 → 업데이트 인정
                if (newIds.Except(previousIds).Any() || previousIds.Except(newIds).Any())
                    return list;

                await Task.Delay(500);
            }

            return LoadInputDevices();
        }
        private async Task<List<DeviceItem>> WaitForStableOutputDevices(List<string> previousIds, int retry = 5)
        {
            for (int i = 0; i < retry; i++)
            {
                var list = LoadOutputDevices();
                var newIds = list.Select(d => d.ID).ToList();

                // 새로운 장치 ID가 존재하는 경우 → 업데이트 인정
                if (newIds.Except(previousIds).Any() || previousIds.Except(newIds).Any())
                    return list;

                await Task.Delay(500);
            }

            return LoadOutputDevices();
        }


        // ──────────────── [장치 전환] ────────────────

        public async Task SetSelectedInputDeviceAsync(DeviceItem value)
        {
            if (value == null || string.IsNullOrWhiteSpace(value.ID))
            {
                Logger.Debug("초기 상태로 인해 장치 전환 생략");
                return;
            }

            _selectedInputDevice = value;
            OnPropertyChanged(nameof(SelectedInputDevice));

            await DeviceManager.SetDeviceAsync(new DeviceItem
            {
                ID = value.ID,
                Name = value.Name
            }, DataFlow.Capture);

            LoadInputVolume();
            LoadInputMute();
            LoadInputLevelMonitoring();

            Alert.Show("INFO", $"{value.Name} ");
        }

        public async Task SetSelectedOutputDeviceAsync(DeviceItem value)
        {
            if (value == null || string.IsNullOrWhiteSpace(value.ID))
            {
                Logger.Debug("초기 상태로 인해 장치 전환 생략");
                return;
            }

            _selectedOutputDevice = value;
            OnPropertyChanged(nameof(SelectedOutputDevice));
            // 장치 전환 실행
            await DeviceManager.SetDeviceAsync(new DeviceItem
            {
                ID = value.ID,
                Name = value.Name
            }, DataFlow.Render);

            LoadOutputVolume();
            LoadOutputMute();
            LoadOutputLevelMonitoring();

            Alert.Show("INFO", $"{value.Name} ");
        }


        // ──────────────── [음성 입출력 모니터링] ────────────────

        public void OnRendering(object? sender, EventArgs e)
        {
            // 부드럽게 보간 - Clamp - View 바인딩용 값 갱신
            _smoothedInputVolumeLevel += (InputVolumeLevel - _smoothedInputVolumeLevel) * VolumeSmoothingFactor;
            _smoothedInputVolumeLevel = Math.Clamp(_smoothedInputVolumeLevel, 0f, 1f);
            SmoothedInputVolumeLevel = _smoothedInputVolumeLevel;

            _smoothedOutputVolumeLevel += (OutputVolumeLevel - _smoothedOutputVolumeLevel) * VolumeSmoothingFactor;
            _smoothedOutputVolumeLevel = Math.Clamp(_smoothedOutputVolumeLevel, 0f, 1f);
            SmoothedOutputVolumeLevel = _smoothedOutputVolumeLevel;
        }

        private void LoadInputLevelMonitoring()
        {
            if (SelectedInputDevice == null)
            {
                Logger.Warn("선택된 입력 장치가 없습니다.");
                return;
            }

            try
            {
                _inputCapture?.StopRecording();
                _inputCapture?.Dispose();
                _inputCapture = null;
            }
            catch (Exception ex)
            {
                Logger.Warn($"InputCapture 정지 중 예외 발생: {ex.Message}");
            }

            try
            {
                // 현재 선택된 입력 장치 ID 기준으로 MMDevice 가져오기
                MMDevice? nowInput = DeviceService.Instance.GetInputDeviceById(SelectedInputDevice.ID);
                if (nowInput == null)
                {
                    Logger.Warn("입력 장치 정보를 찾을 수 없습니다.");
                    return;
                }

                _inputCapture = new WasapiCapture(nowInput);
                _inputCapture.DataAvailable += (s, e) =>
                {
                    float localPeak = 0;

                    for (int i = 0; i < e.BytesRecorded; i += 4)
                    {
                        float sample = BitConverter.ToSingle(e.Buffer, i);
                        float sampleAbs = Math.Abs(sample);
                        if (sampleAbs > localPeak)
                            localPeak = sampleAbs;
                    }

                    InputVolumeLevel = localPeak; // 0.0 ~ 1.0
                };

                _inputCapture.StartRecording();
            }
            catch (Exception ex)
            {
                Logger.Error($"입력 레벨 측정 시작 중 오류 발생: {ex.Message}");
            }
        }

        private void LoadOutputLevelMonitoring()
        {
            if (SelectedOutputDevice == null)
            {
                Logger.Warn("선택된 출력 장치가 없습니다.");
                return;
            }

            try
            {
                _outputCapture?.StopRecording();
                _outputCapture?.Dispose();
                _outputCapture = null;
            }
            catch (Exception ex)
            {
                Logger.Warn($"InputCapture 정지 중 예외 발생: {ex.Message}");
            }

            try
            {
                // 현재 선택된 입력 장치 ID 기준으로 MMDevice 가져오기
                MMDevice? nowOutput = DeviceService.Instance.GetOutputDeviceById(SelectedOutputDevice.ID);
                if (nowOutput == null)
                {
                    Logger.Warn("입력 장치 정보를 찾을 수 없습니다.");
                    return;
                }

                _outputCapture = new WasapiLoopbackCapture(nowOutput);
                _outputCapture.DataAvailable += (s, e) =>
                {
                    float localPeak = 0;

                    for (int i = 0; i < e.BytesRecorded; i += 4)
                    {
                        float sample = BitConverter.ToSingle(e.Buffer, i);
                        float sampleAbs = Math.Abs(sample);
                        if (sampleAbs > localPeak)
                            localPeak = sampleAbs;
                    }

                    OutputVolumeLevel = localPeak; // 0.0 ~ 1.0
                };

                _outputCapture.StartRecording();
            }
            catch (Exception ex)
            {
                Logger.Error($"입력 레벨 측정 시작 중 오류 발생: {ex.Message}");
            }
        }


        // ──────────────── [장치 볼륨/음소거 설정] ────────────────

        private void LoadInputVolume()
        {
            if (SelectedInputDevice != null)
                InputVolume = DeviceService.Instance.GetInputVolume();
        }
        private void LoadOutputVolume()
        {
            if (SelectedOutputDevice != null)
                OutputVolume = DeviceService.Instance.GetOutputVolume();
        }
        private void LoadInputMute()
        {
            if (SelectedInputDevice != null)
                IsInputMuted = DeviceService.Instance.GetInputMute();
        }
        private void LoadOutputMute()
        {
            if (SelectedOutputDevice != null)
                IsOutputMuted = DeviceService.Instance.GetOutputMute();
        }



        // ──────────────── [타이머] ────────────────

        private void StartTimerLoop()
        {
            _elapsedTime = TimeSpan.Zero;
            TimerText = "00:00:00";
            _timer?.Start();
        }

        private void StopTimerLoop()
        {
            _timer?.Stop();
            _elapsedTime = TimeSpan.Zero;
            TimerText = "00:00:00";
        }



        // ──────────────── [녹음 시작] ────────────────

        private async void StartRecording()
        {
            // comment 작업까지 하고 마무리
            string randomString = RandomStringGenerator.Generate(6);
            string now = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            string key = now + "_1_" + randomString.ToUpper();
            string subkey = "1";

            string fileName = key;

            IniFile _ini = new IniFile(Path.Combine(AppContext.BaseDirectory, "config.ini"));
            string? ip = _ini.Get("Server", "Host");
            string? port = _ini.Get("Server", "Port");
            string? filePath = _ini.Get("Server", "Path");
            string? _fileFormat = _ini.Get("Recorder", "Format");
            if (string.IsNullOrEmpty(_fileFormat)) _fileFormat = "2"; // 디폴트 파일 포맷 (mp3)
            int fileFormat = int.Parse(_fileFormat);

            if (string.IsNullOrEmpty(filePath)) filePath = "C:/VoiceLog/LocalData/Rec/"; // 디폴트 저장 경로

            if (string.IsNullOrEmpty(ip) || string.IsNullOrEmpty(port))
            {
                Alert.Show("ERROR", "서버 정보가 누락되었습니다. 설정을 확인하세요");
                return;
            }

            _voiceManager.SetEngineUrl(1, ip, int.Parse(port));
            _voiceManager.SetRequiredData(key, subkey, filePath, fileName, fileFormat);

            bool start = await _voiceManager.StartRecordingAsync();

            _voiceManager.PushStatus(200, "open", "recording");

            StartRecordingCallback(start);

        }
        public void StartRecordingCallback(bool start)
        {
            if (start)
            {
                IsRecording = true;


                string host = _voiceManager.GetHost();
                int port = _voiceManager.GetPort();
                if (host != "" && port != 0)
                {
                    if (CommentMessages == null)
                    {
                        Logger.Warn($"CommentWorker 객체가 초기화 되지 않았습니다. ");
                    }
                    else
                    {
                        CommentMessages.Clear();
                        // 채팅 워커 초기화
                        commentWorker = new CommentWorker(_voiceManager.GetHost(), _voiceManager.GetPort());

                        Logger.Info($"CommentWorker 연결 성공 - {host} : {port}");
                    }
                }
                else
                {
                    Logger.Warn($"CommentWorker 초기화 실패 .. 서버 정보가 누락되었습니다. ");
                }
                Alert.Show("INFO", "녹음 시작!");
                Logger.Info("[*] Record Start! ");

                if (Application.Current?.MainWindow is MainWindow mainWindow)
                {
                    LoginInfo info = mainWindow.CurrentUserInfo;
                    Logger.Info($"[*] Current User Info  - [ID] : {info.UserId} [PW] : {info.Password}");
                }

                StartTimerLoop();
            }
            else
            {
                Alert.Show("ERROR", "녹음을 시작하지 못했습니다. 서버 연결을 확인하세요");
            }
        }




        // ──────────────── [녹음 종료] ────────────────
        public void StopRecording()
        {
            bool stopOk = _voiceManager.StopRecording();

            _voiceManager.PushStatus(200, "open", "stop");

            StopRecordingCallback(stopOk);
        }

        public void StopRecordingCallback(bool stop)
        {
            if (stop)
            {
                IsRecording = false;
                commentWorker = null;
                Alert.Show("INFO", "녹음 종료!");
                Logger.Info("[*] Record Stop!");


                StopTimerLoop();
            }
            else
            {
                Alert.Show("ERROR", "녹음 종료 실패 .. ");
                Logger.Info("[*] Record Stop!");
            }
        }



        // ──────────────── [코멘트 관리] ────────────────
        public async Task SubmitCommentAsync()
        {
            string? recKey = _voiceManager.GetRecKey();
            string? subKey = _voiceManager.GetSubKey();

            if (string.IsNullOrEmpty(recKey) || string.IsNullOrEmpty(subKey))
            {
                Alert.Show("ERROR", "녹취 중이 아닙니다.");
                return;
            }

            string comment = InputCommentText.Trim();
            if (string.IsNullOrWhiteSpace(comment)) return;

            if (commentWorker == null)
            {
                Logger.Error("전송 프로세스가 초기화되지 않았습니다.");
                Alert.Show("ERROR", "전송 프로세스가 초기화되지 않았습니다.");
                return;
            }

            var userInfo = LoginConfigService.Load();
            if (userInfo == null)
            {
                Logger.Error("로그인 정보가 누락되었습니다.");
                return;
            }

            MessageCommentSend message = new MessageCommentSend
            {
                UserId = userInfo.UserId,
                Comment = comment,
                RecKey = recKey,
                SubKey = subKey
            };

            bool insertOk = await commentWorker.Send(message);
            if (insertOk)
            {
                if (CommentMessages != null)
                {
                    CommentMessages.Add(new Comment
                    {
                        Text = comment,
                        Time = DateTime.Now
                    });

                    InputCommentText = "";
                }
                else
                {
                    Alert.Show("ERROR", "코멘트 객체가 초기화 되지 않았습니다. ");
                }
            }
            else
            {
                Alert.Show("ERROR", "[서버 오류] 입력에 실패하였습니다. ");
            }

        }


        // ──────────────── [탭 전환] ────────────────
        private void ToggleTab(int tabIndex)
        {
            if (IsSubAreaVisible && SelectedTabIndex == tabIndex)
            {
                // 동일 탭 → 닫기
                PlayCloseAnimation = true;
                PlayOpenAnimation = false;
                IsSubAreaVisible = false;
            }
            else
            {
                SelectedTabIndex = tabIndex;
                IsSubAreaVisible = true;
                PlayCloseAnimation = false;
                PlayOpenAnimation = true;
            }
        }

    }
}