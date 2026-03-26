using NAudio.CoreAudioApi;
using NAudio.Lame;
using NAudio.Wave;
using RecordClient.Helpers;
using RecordClient.Helpers.Config;
using RecordClient.Services.Login;
using RecordClient.Services.Sending;
using RecordClient.Services.Server;
using System.IO;
using static RecordClient.Services.Record.VoiceManager;
using DeviceState = NAudio.CoreAudioApi.DeviceState;

namespace RecordClient.Services.Record
{
    public class AudioManager
    {
        public static bool isRecording = false; // 주 장치 녹음 중 여부
        public static bool isRecordingSub = false; // 보조 장치 녹음 중 여부

        private volatile bool mv_audioPaused = false; // 장치 일시중지 여부

        private static readonly string TAG = nameof(AudioManager);

        private VoiceManager? m_voiceManager; // 음성 관리 객체

        // 녹음 처리 스레드
        private Thread? m_audioThread;
        private bool mv_audioRunning = false; // 현재 녹음 상태 여부

        // 데이터 전송 스레드
        private Thread? m_sendThread;
        private bool mv_sendRunning = false; // 현재 전송 진행 중 여부

        // 레코더 객체
        private List<WaveInEvent>? m_audioRecorders; // 오디오 입력 장치
        private WaveFileWriter? waveFile; // 오디오 파일 저장 객체

        // 레코더 정보
        private int mv_audioChannel = 1;
        private int mv_fileFormat = 1; // 1: WAV/GSM, 2: MP3
        private int mv_sampleRate = 8000; // 샘플링 속도 (Hz)
        private int mv_minBuffer = 0; // 최소 버퍼 크기 
        private int mv_mute = 0; // 묵음 여부

        // 파일 정보
        private bool mv_isDevideChannel = false; // 채널 분리 저장 여부
        private bool mv_isSubRecorder = false; // 보조 장치 사용 여부

        private string mv_mainMicName = "";
        private string mv_subMicName = "ENC";
        //private string mv_subMicName = "Lenovo Performance Camera";
        //private string mv_subMicName = "Lenovo";

        // Mix / Mono
        private FileStream? m_fileStream = null; // 로컬파일 스트림 객체 (전체)
        private FileInfo? m_file = null; // 로컬파일 객체
        private LameMP3FileWriter? m_lame = null; // MP3 인코더

        // Stereo
        private FileStream? m_fileStream_ch1 = null;
        private FileStream? m_fileStream_ch2 = null;
        private FileInfo? m_file_ch1 = null;
        private FileInfo? m_file_ch2 = null;
        private LameMP3FileWriter? m_lame_ch1 = null;
        private LameMP3FileWriter? m_lame_ch2 = null;

        private List<LinkedList<byte[]>>? m_outBuffer; // 음성 데이터 버퍼

        private string mv_recKey = "";      // 녹취 키
        private string mv_subKey = "";      // 녹취 순번
        private string mv_filePath = "";    // 파일 경로
        private string mv_fileName = "";    // 파일 명
        private int mv_fileSize = 0;        // 녹취파일 사이즈

        // 엔진 정보
        private bool mv_isSaveLocal = false; // 로컬 파일 보관 여부
        private bool mv_isSendServer = true; // 데이터 패킷 전송 여부

        private TCPSendData? mv_recServer = null; // 메인 서버 (메인패킷)
        private TCPSendData? mv_recServer_sub = null; // 메인 서버 (채널분리 보조패킷)
        private string mv_engineUrl = "";   // ( Main Server ) 패킷 전송할 엔진 주소
        private int mv_enginePort = 0;      // ( Main Server ) 패킷 전송할 엔진 포트

        private TCPSendData? mv_recServer_2 = null; // 서브 서버 (이중화) (메인패킷)
        private TCPSendData? mv_recServer_2_sub = null; // 서브 서버 (이중화) (채널분리 보조패킷)
        private string mv_engineUrl_2 = ""; // ( Sub Server ) 패킷 전송할 엔진 주소
        private int mv_enginePort_2 = 0;    // ( Sub Server ) 패킷 전송할 엔진 포트

        public AudioManager()
        {
            if (m_voiceManager == null)
            {
                m_voiceManager = VoiceManager.GetInstance();
            }
        }

        /*
        * @Description 녹취 시작
        */
        public bool Start()
        {
            if (m_audioRecorders == null || mv_audioRunning)
            {
                Logger.Error($"[{TAG}] 녹음이 이미 실행 중이거나 초기화되지 않았습니다.");
                return false;
            }

            lock (this) // 동시성 방지
            {
                if (m_audioThread != null && m_audioThread.IsAlive)
                {
                    Logger.Error($"[{TAG}] 오디오 스레드가 이미 실행 중입니다.");
                    return false;
                }

                // 1번 서버 연결
                if (mv_recServer == null && !string.IsNullOrEmpty(mv_engineUrl))
                {
                    mv_recServer = new TCPSendData(mv_engineUrl, mv_enginePort, false, m_voiceManager);
                }

                // 2번 서버 연결
                if (mv_recServer_2 == null && !string.IsNullOrEmpty(mv_engineUrl_2))
                {
                    mv_recServer_2 = new TCPSendData(mv_engineUrl_2, mv_enginePort_2, false, m_voiceManager);
                }

                m_audioThread = new Thread(new AudioThread(this).Run)
                {
                    Name = "Thread-Audio"
                };

                m_audioThread.Start();

                return true;
            }
        }

        /*
        * @Description 녹취 종료
        */
        public bool Stop()
        {
            if (mv_audioRunning)
            {
                Logger.Info($"[{TAG}] 오디오 스레드 종료...");

                mv_audioRunning = false;

                if (m_audioThread != null)
                {
                    if (m_audioRecorders != null)
                    {
                        try
                        {
                            foreach (var _recorder in m_audioRecorders)
                            {
                                _recorder.StopRecording();
                            }
                        }

                        catch (Exception ex)
                        {
                            foreach (var _recorder in m_audioRecorders)
                            {
                                _recorder.Dispose();
                            }
                            Logger.Error($"[{TAG}] WaveInEvent 중지 중 오류 발생: {ex.Message} ");
                        }

                        m_audioRecorders.Clear();
                    }
                    if (m_audioThread.IsAlive)
                    {
                        try
                        {
                            m_audioThread.Join(1000); // 1초(1000ms) 동안 대기 후 종료
                            Logger.Info($"[{TAG}] 오디오 스레드 대기 후 종료!");
                        }
                        catch (ThreadInterruptedException e)
                        {
                            Logger.Error($"[{TAG}] 오디오 스레드 중지 중 오류 발생: {e.Message}");
                        }
                    }
                    else
                    {
                        Logger.Info($"[{TAG}] 오디오 스레드 종료!");
                    }
                }
                else
                {
                    Logger.Info($"[{TAG}] 이미 종료 된 스레드입니다.");
                }
            }
            else
            {
                Logger.Info($"[{TAG}] 레코더가 시작되지 않았습니다.");
            }

            m_audioThread = null;
            return true;
        }

        /// <summary>
        /// 녹취 일시정지
        /// </summary>
        public bool Pause()
        {
            if (!mv_audioRunning)
            {
                Logger.Debug($"[{TAG}] 현재 녹음 중이 아닙니다.");
                return false;
            }
            else
            {
                if (!mv_audioPaused)
                {
                    mv_audioPaused = true;
                    Logger.Info($"[{TAG}] 녹음이 일시 중지되었습니다.");
                    return true;

                }
                else
                    return false;
            }
        }

        public bool Resume()
        {
            if (mv_audioRunning && mv_audioPaused)
            {
                mv_audioPaused = false;
                Logger.Info($"[{TAG}] 녹음이 재개되었습니다.");
                return true;
            }
            else
            {
                return false;
            }
        }

        /**
         * @Description 오디오 포맷 세팅
         * 
         * @Param
         * int fileFormat - 파일 포맷 ( 1: WAV/GSM, 2: MP3 )
         */
        public bool initRecorder(int fileFormat)
        {
            bool pass;
            pass = Init(); // 맴버 변수 초기화

            if (!pass) return false;

            mv_fileFormat = fileFormat;

            // 코덱/확장자별 샘플링 속도 설정
            if (mv_fileFormat == 1) // WAV/GSM
            {
                mv_sampleRate = 16000;
            }
            else if (mv_fileFormat == 2) // MP3
            {
                mv_sampleRate = 16000;
            }
            else // WAV/PCM
            {
                mv_sampleRate = 16000;
            }

            var enumerator = new MMDeviceEnumerator();

            var devices = enumerator.EnumerateAudioEndPoints(DataFlow.Capture, DeviceState.Active).ToList(); // 디바이스 리스트
            var mainDevice = enumerator.GetDefaultAudioEndpoint(DataFlow.Capture, Role.Multimedia); // 선택 된 주장치

            // 버퍼 크기 설정
            mv_minBuffer = mv_sampleRate / 10; // 기본 버퍼 크기 설정

            m_audioRecorders = new List<WaveInEvent>();
            m_outBuffer = new List<LinkedList<byte[]>>();

            // 주장치로 레코더 생성
            WaveInEvent _mainRecorder = new WaveInEvent
            {
                DeviceNumber = 0,
                WaveFormat = new WaveFormat(mv_sampleRate, 16, 2),
                BufferMilliseconds = 300
            };

            _mainRecorder.DataAvailable += (s, e) => OnDataAvailable(_mainRecorder, e);
            _mainRecorder.RecordingStopped += (s, e) => OnRecordingStopped(_mainRecorder, e);

            m_audioRecorders.Add(_mainRecorder);
            m_outBuffer.Add(new LinkedList<byte[]>());

            Logger.Debug($"[{TAG}] 주 레코더 객체 생성 완료 - {mainDevice.FriendlyName}");
            Logger.Debug($"[{TAG}] * Format: {_mainRecorder.WaveFormat.Encoding}, Channels: {_mainRecorder.WaveFormat.Channels}, SampleRate: {_mainRecorder.WaveFormat.SampleRate}");

            var format = _mainRecorder.WaveFormat;
            mv_audioChannel = format.Channels;

            // 보조장치 조회 (화자분리용 마이크칩)
            var secondaryDevice = devices.FirstOrDefault(d => d.FriendlyName.Contains(mv_subMicName));

            if (secondaryDevice != null)
            {
                int deviceIndex = devices.IndexOf(secondaryDevice);
                if (deviceIndex >= 0)
                {
                    mv_isSubRecorder = true;
                    mv_isDevideChannel = true;

                    WaveInEvent _subRecorder = new WaveInEvent
                    {
                        DeviceNumber = deviceIndex,
                        WaveFormat = new WaveFormat(mv_sampleRate, 16, 2),
                        BufferMilliseconds = 300
                    };
                    _subRecorder.DataAvailable += (s, e) => OnDataAvailableSub(_subRecorder, e);
                    _subRecorder.RecordingStopped += (s, e) => OnRecordingStoppedSub(_subRecorder, e);

                    m_audioRecorders.Add(_subRecorder);
                    m_outBuffer.Add(new LinkedList<byte[]>());

                    Logger.Debug($"[{TAG}] 보조 레코더(ENC) 객체 생성 완료 - {secondaryDevice.FriendlyName}");
                    Logger.Debug($"[{TAG}] * Recorder Format: {_subRecorder.WaveFormat.Encoding}, Channels: {_subRecorder.WaveFormat.Channels}, SampleRate: {_subRecorder.WaveFormat.SampleRate}");

                    if (mv_recServer != null)
                    {
                        Logger.Info($"[{TAG}] 1번 서버 보조소켓 연결...");
                        mv_recServer_sub = new TCPSendData(mv_engineUrl, mv_enginePort, false, null);
                    }

                    if (mv_recServer_2 != null)
                    {
                        Logger.Info($"[{TAG}] 2번 서버 보조소켓 연결...");
                        mv_recServer_2_sub = new TCPSendData(mv_engineUrl_2, mv_enginePort_2, false, null);
                    }

                }
            }
            return true;
        }

        /**
         * @Description 오디오 관리자 초기화
         */
        private bool Init()
        {
            try
            {
                if (m_outBuffer != null)
                {
                    foreach (var buffer in m_outBuffer)
                    {
                        buffer.Clear();
                    }
                    m_outBuffer.Clear();
                }

                if (m_audioRecorders != null)
                {
                    foreach (var _recorder in m_audioRecorders)
                    {
                        _recorder.StopRecording();
                        _recorder.Dispose();
                    }
                    m_audioRecorders.Clear();
                }

                InitFileStream();
                InitFileObj();
            }
            catch (IOException e)
            {
                Logger.Error($"[{TAG}] 레코더 초기화 오류: {e.Message}");
                return false;
            }

            Logger.Debug($"[{TAG}] 레코더 초기화 완료!");

            return true;
        }

        private void InitFileStream()
        {
            try
            {
                // Mix 파일 정보 닫기
                if (m_fileStream != null)
                {
                    m_fileStream.Close();
                    m_fileStream.Dispose();
                    m_fileStream = null;
                }

                if (mv_fileFormat == 2 && m_lame != null)
                {
                    m_lame.Close();
                    m_lame.Dispose();
                    m_lame = null;
                }

                // Stereo 파일 정보 닫기
                if (m_fileStream_ch1 != null)
                {
                    m_fileStream_ch1.Close();
                    m_fileStream_ch1.Dispose();
                    m_fileStream_ch1 = null;
                }

                if (mv_fileFormat == 2 && m_lame_ch1 != null)
                {
                    m_lame_ch1.Close();
                    m_lame_ch1.Dispose();
                    m_lame_ch1 = null;
                }


                if (m_fileStream_ch2 != null)
                {
                    m_fileStream_ch2.Close();
                    m_fileStream_ch2.Dispose();
                    m_fileStream_ch2 = null;
                }

                if (mv_fileFormat == 2 && m_lame_ch2 != null)
                {
                    m_lame_ch2.Close();
                    m_lame_ch2.Dispose();
                    m_lame_ch2 = null;
                }

                Thread.Sleep(2100); // 파일이 닫힐 시간을 확보
            }
            catch (Exception e)
            {
                Logger.Error($"[{TAG}] 파일 스트림 후처리 중 오류 발생: {e.StackTrace}");
            }

        }

        private void InitFileObj()
        {

            // 파일 삭제 (임시 파일을 활용 할 경우 해당 위치에 소스 작성 후 삭제 하면된다.)
            try
            {
                if (m_file != null && m_file.Exists)
                {
                    using (FileStream fs = new FileStream(m_file.FullName, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
                    {
                        m_file.Delete();
                        m_file = null;
                    }
                }

                if (m_file_ch1 != null && m_file_ch1.Exists)
                {
                    using (FileStream fs = new FileStream(m_file_ch1.FullName, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
                    {
                        m_file_ch1.Delete();
                        m_file_ch1 = null;
                    }
                }

                if (m_file_ch2 != null && m_file_ch2.Exists)
                {
                    using (FileStream fs = new FileStream(m_file_ch2.FullName, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
                    {
                        m_file_ch2.Delete();
                        m_file_ch2 = null;
                    }
                }

            }
            catch (IOException ioe)
            {
                Logger.Error($"[{TAG}] 파일 후처리 중 I/O 오류 발생: {ioe.StackTrace}");
            }
            catch (Exception e)
            {
                Logger.Error($"[{TAG}] 파일 후처리 중 일반 오류 발생: {e.StackTrace}");
            }

            m_file = null;
            m_file_ch1 = null;
            m_file_ch2 = null;
        }

        /**
         * @Description 녹음 시작 후 음성 들어올 때마다의 콜백 함수
         */
        private void OnDataAvailable(WaveInEvent recorder, WaveInEventArgs e)
        {
            if (!mv_audioRunning || mv_audioPaused)
                return;

            if (!isRecording)
            {
                Logger.Info($"[{TAG}] 주장치 오디오 녹음 시작!");
                isRecording = true;
            }

            if (m_audioRecorders == null || m_outBuffer == null) return;

            var format = m_audioRecorders[0].WaveFormat;
            int channels = format.Channels;

            try
            {
                // 1. 저장 처리
                if (mv_isSaveLocal)
                {
                    if (mv_fileFormat == 1) // WAV
                    {
                        if (m_fileStream != null) m_fileStream.Write(e.Buffer, 0, e.BytesRecorded);
                    }
                    else if (mv_fileFormat == 2) // MP3
                    {
                        if (e.BytesRecorded > e.Buffer.Length)
                        {
                            return;
                        }

                        if (m_lame != null) m_lame.Write(e.Buffer, 0, e.BytesRecorded);
                    }
                }
                // 여기
                // 2. 버퍼 저장
                if (m_outBuffer.Count > 0)
                {
                    lock (m_outBuffer[0])
                    {
                        m_outBuffer[0].AddLast(e.Buffer);
                    }
                }

            }
            catch (Exception ex)
            {
                Logger.Error($"[{TAG}] 파일 스트림 쓰기 실패: {ex.Message}");
            }
        }

        private void OnDataAvailableSub(WaveInEvent recorder, WaveInEventArgs e)
        {
            if (!mv_audioRunning || mv_audioPaused)
                return;

            if (!isRecordingSub)
            {
                Logger.Info($"[{TAG}] 보조장치 오디오 녹음 시작!");
                isRecordingSub = true;
            }

            if (m_audioRecorders == null || m_outBuffer == null) return;

            var format = m_audioRecorders[1].WaveFormat;
            int channels = format.Channels;

            try
            {
                if (mv_isSaveLocal && channels == 2)
                {
                    // 1. 저장 처리
                    if (mv_fileFormat == 1) // WAV
                    {
                        SplitChannelsAndWrite(e.Buffer, e.BytesRecorded, isMp3: false);
                    }
                    else if (mv_fileFormat == 2) // MP3
                    {
                        SplitChannelsAndWrite(e.Buffer, e.BytesRecorded, isMp3: true);
                    }
                }

                // 2. 버퍼 저장
                if (m_outBuffer.Count > 1)
                {
                    lock (m_outBuffer[1])
                    {
                        m_outBuffer[1].AddLast(e.Buffer);
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"[{TAG}] 파일 스트림 쓰기 실패: {ex.Message}");
            }
        }

        private byte[] channelOverBytes = Array.Empty<byte>();

        private void SplitChannelsAndWrite(byte[] inputBuffer, int bytesRecorded, bool isMp3)
        {
            byte[] channelBytes = channelOverBytes;

            byte[] fullBuffer = new byte[channelBytes.Length + bytesRecorded];
            Buffer.BlockCopy(channelBytes, 0, fullBuffer, 0, channelBytes.Length);
            Buffer.BlockCopy(inputBuffer, 0, fullBuffer, channelBytes.Length, bytesRecorded);

            int bytesPerSample = 2; // 16bit = 2byte
            int bytesPerFrame = 4; // 2byte L + 2byte R
            int totalFrames = fullBuffer.Length / bytesPerFrame;
            int validLength = totalFrames * bytesPerFrame;

            byte[] buffer_ch1 = new byte[totalFrames * bytesPerSample];
            byte[] buffer_ch2 = new byte[totalFrames * bytesPerSample];

            for (int i = 0; i < totalFrames; i++)
            {
                int srcIndex = i * 4;

                // Left 채널 (CH1)
                buffer_ch1[i * 2] = fullBuffer[srcIndex];
                buffer_ch1[i * 2 + 1] = fullBuffer[srcIndex + 1];

                // Right 채널 (CH2)
                buffer_ch2[i * 2] = fullBuffer[srcIndex + 2];
                buffer_ch2[i * 2 + 1] = fullBuffer[srcIndex + 3];
            }

            if (isMp3)
            {
                m_lame_ch1?.Write(buffer_ch1, 0, buffer_ch1.Length);
                m_lame_ch2?.Write(buffer_ch2, 0, buffer_ch2.Length);
            }
            else
            {
                m_fileStream_ch1?.Write(buffer_ch1, 0, buffer_ch1.Length);
                m_fileStream_ch2?.Write(buffer_ch2, 0, buffer_ch2.Length);
            }

            int leftover = fullBuffer.Length - validLength;
            channelBytes = new byte[leftover];

            channelOverBytes = channelBytes;
            Buffer.BlockCopy(fullBuffer, validLength, channelOverBytes, 0, leftover);
        }

        /**
         * @Description 녹음 종료 후 콜백 함수
         */
        private void OnRecordingStopped(WaveInEvent recorder, StoppedEventArgs ex)
        {
            Logger.Info($"[{TAG}] 주 장치 녹음 중지!");

            isRecording = false;

            closeRecordFile(); // 로컬파일 종료
            SendStop(); // 녹취 엔진 패킷 전송 종료

            if (recorder != null)
            {
                recorder.Dispose();
            }
        }

        private void OnRecordingStoppedSub(WaveInEvent recorder, StoppedEventArgs ex)
        {
            Logger.Info($"[{TAG}] 보조 장치 녹음 중지!");

            isRecordingSub = false;

            if (recorder != null)
            {
                recorder.Dispose();
            }
        }

        /*
        * @Description 오디오 스레드
        */
        class AudioThread
        {
            private AudioManager _audioManager;

            public AudioThread(AudioManager audioManager)
            {
                _audioManager = audioManager;
            }

            public void Run()
            {
                Logger.Debug($"[{TAG}] 오디오 스레드 시작!");

                _audioManager.audioWorker();
            }
        }

        /*
        * @Description 녹음 시작 워커
        */
        private void audioWorker()
        {
            if (m_audioRecorders != null)
            {
                mv_audioRunning = true;

                // 0번은 주 레코더, 1번은 보조 레코더
                for (int i = 0; i < m_audioRecorders.Count; i++)
                {
                    WaveInEvent _recorder = m_audioRecorders[i];

                    createRecordFile(_recorder, i); // 로컬에 파일 생성
                    _recorder.StartRecording(); // 녹음 시작
                }

                SendStart(); // 녹취 엔진 패킷 전송 시작
            }
            else
            {
                if (m_voiceManager != null) m_voiceManager.AudioClose();
            }
        }

        /*
        * @Description 로컬에 파일 생성
        */
        private bool createRecordFile(WaveInEvent recorder, int index)
        {
            if (mv_isSaveLocal)
            {
                string basePath = Path.Combine(mv_filePath, mv_fileName);
                string ext = mv_fileFormat == 2 ? ".mp3" : ".wav";

                try
                {
                    Directory.CreateDirectory(mv_filePath);

                    if (index == 0)
                    {
                        // 메인 파일 스트림 생성
                        var file = new FileInfo(basePath + ext);
                        var fs = new FileStream(file.FullName, FileMode.Create, FileAccess.ReadWrite);

                        Logger.Info($"[{TAG}] 녹취 파일 경로 - Path: {mv_filePath}{mv_fileName}");

                        // 메인 파일 Writer
                        if (mv_fileFormat == 2)
                        {
                            var lame = new LameMP3FileWriter(fs, recorder.WaveFormat, 64);
                            m_lame = lame;
                            m_file = file;
                        }
                        m_file = file;
                        m_fileStream = fs;
                    }
                    else if (index == 1 && mv_isDevideChannel)
                    {
                        var monoFormat = new WaveFormat(recorder.WaveFormat.SampleRate, 16, 1);

                        // CH1
                        var file_ch1 = new FileInfo(basePath + "_CH1" + ext);
                        var fs_ch1 = new FileStream(file_ch1.FullName, FileMode.Create, FileAccess.ReadWrite);
                        var lame_ch1 = mv_fileFormat == 2 ? new LameMP3FileWriter(fs_ch1, monoFormat, 64) : null;

                        // CH2
                        var file_ch2 = new FileInfo(basePath + "_CH2" + ext);
                        var fs_ch2 = new FileStream(file_ch2.FullName, FileMode.Create, FileAccess.ReadWrite);
                        var lame_ch2 = mv_fileFormat == 2 ? new LameMP3FileWriter(fs_ch2, monoFormat, 64) : null;

                        m_file_ch1 = file_ch1;
                        m_file_ch2 = file_ch2;
                        m_fileStream_ch1 = fs_ch1;
                        m_fileStream_ch2 = fs_ch2;
                        m_lame_ch1 = lame_ch1;
                        m_lame_ch2 = lame_ch2;

                        Logger.Info($"[{TAG}] 1번 채널 파일 생성: {file_ch1.Exists}");
                        Logger.Info($"[{TAG}] 2번 채널 파일 생성: {file_ch2.Exists}");
                    }

                    return true;
                }
                catch (FileNotFoundException e)
                {
                    Logger.Error($"[{TAG}] 파일 열기 실패: {e.Message}");
                    return false;
                }
                catch (Exception e)
                {
                    Logger.Error($"[{TAG}] 예외 발생: {e.Message}");
                    return false;
                }
            }
            return false;
        }


        /*
        * @Description 로컬파일 종료 및 제거
        */
        private void closeRecordFile()
        {
            if (mv_fileFormat == 2) // MP3 후처리
            {
                SafeClose(m_lame, "MAIN");
                SafeClose(m_lame_ch1, "CH1");
                SafeClose(m_lame_ch2, "CH2");
            }
            else
            {
                ProcessWavFinalization(m_fileStream, "MAIN");
                ProcessWavFinalization(m_fileStream_ch1, "CH1");
                ProcessWavFinalization(m_fileStream_ch2, "CH2");
            }

            Thread.Sleep(200); // 파일이 닫힐 시간을 확보

            InitFileStream();
            InitFileObj();
        }

        private void SafeClose(LameMP3FileWriter? writer, string label)
        {
            if (writer == null)
            {
                return;
            }

            try
            {
                writer.Flush();
                writer.Dispose();
                writer.Close();
            }
            catch (Exception ex)
            {
                Logger.Error($"[{TAG}] MP3 Writer 닫기 실패 - {label}: {ex.Message}");
            }
        }

        private void ProcessWavFinalization(FileStream? stream, string label)
        {
            if (stream == null)
            {
                return;
            }

            try
            {
                stream.Seek(0, SeekOrigin.Begin);
                byte[] pcmData = new byte[stream.Length];
                stream.Read(pcmData, 0, pcmData.Length);

                byte[] wavHeader = GetWavFileHeader(pcmData.Length);

                stream.SetLength(0);
                stream.Seek(0, SeekOrigin.Begin);

                stream.Write(wavHeader, 0, wavHeader.Length);
                stream.Write(pcmData, 0, pcmData.Length);

                stream.Flush();
                stream.Dispose();
            }
            catch (Exception ex)
            {
                Logger.Error($"[{TAG}] WAV Writer 닫기 실패 {label}: {ex.Message}");
            }
        }


        private byte[] GetWavFileHeader(int audioLength)
        {
            int codecType = 1; // 1: PCM
            int channelType = mv_audioChannel; // 1: Mono, 2: Stereo
            int sampleRate = mv_sampleRate;
            int bitsPerSample = 16;

            int totalByteSize = 56;
            int totalDataLen = audioLength + (totalByteSize - 4);

            int blockAlign = bitsPerSample * channelType / 8;
            int byteRate = bitsPerSample * sampleRate * channelType / 8;

            int index = 0;
            byte[] header = new byte[totalByteSize];

            // RIFF
            header[index++] = (byte)'R';
            header[index++] = (byte)'I';
            header[index++] = (byte)'F';
            header[index++] = (byte)'F';

            header[index++] = (byte)(totalDataLen & 0xff);
            header[index++] = (byte)(totalDataLen >> 8 & 0xff);
            header[index++] = (byte)(totalDataLen >> 16 & 0xff);
            header[index++] = (byte)(totalDataLen >> 24 & 0xff);

            header[index++] = (byte)'W';
            header[index++] = (byte)'A';
            header[index++] = (byte)'V';
            header[index++] = (byte)'E';

            // fmt
            header[index++] = (byte)'f';
            header[index++] = (byte)'m';
            header[index++] = (byte)'t';
            header[index++] = (byte)' ';

            header[index++] = 16;
            header[index++] = 0;
            header[index++] = 0;
            header[index++] = 0;

            header[index++] = (byte)codecType;
            header[index++] = 0;

            header[index++] = (byte)channelType;
            header[index++] = 0;

            header[index++] = (byte)(sampleRate & 0xff);
            header[index++] = (byte)(sampleRate >> 8 & 0xff);
            header[index++] = (byte)(sampleRate >> 16 & 0xff);
            header[index++] = (byte)(sampleRate >> 24 & 0xff);

            header[index++] = (byte)(byteRate & 0xff);
            header[index++] = (byte)(byteRate >> 8 & 0xff);
            header[index++] = (byte)(byteRate >> 16 & 0xff);
            header[index++] = (byte)(byteRate >> 24 & 0xff);

            header[index++] = (byte)blockAlign;
            header[index++] = 0;

            header[index++] = (byte)bitsPerSample;
            header[index++] = 0;

            // fact
            header[index++] = (byte)'f';
            header[index++] = (byte)'a';
            header[index++] = (byte)'c';
            header[index++] = (byte)'t';

            header[index++] = 4;
            header[index++] = 0;
            header[index++] = 0;
            header[index++] = 0;

            header[index++] = (byte)(audioLength & 0xff);
            header[index++] = (byte)(audioLength >> 8 & 0xff);
            header[index++] = (byte)(audioLength >> 16 & 0xff);
            header[index++] = (byte)(audioLength >> 24 & 0xff);

            // data
            header[index++] = (byte)'d';
            header[index++] = (byte)'a';
            header[index++] = (byte)'t';
            header[index++] = (byte)'a';

            header[index++] = (byte)(audioLength & 0xff);
            header[index++] = (byte)(audioLength >> 8 & 0xff);
            header[index++] = (byte)(audioLength >> 16 & 0xff);
            header[index++] = (byte)(audioLength >> 24 & 0xff);

            return header;
        }

        /*
        * @Description 패킷 전송 시작
        */
        public bool SendStart()
        {
            if (mv_sendRunning) return false; // 이미 실행 중이면 실행하지 않음

            if (string.IsNullOrEmpty(mv_engineUrl)) return false; // 엔진 호스트 정보가 없으면 실행하지 않음

            if (m_sendThread == null)
            {
                if (mv_isSendServer)
                {
                    m_sendThread = new Thread(new SendThread(this).Run);
                    m_sendThread.Name = "Thread-Sender";
                    m_sendThread.Start();
                }
            }
            return true;
        }

        /*
        * @Description 패킷 전송 종료
        */
        public bool SendStop()
        {
            if (mv_sendRunning)
            {
                Logger.Debug($"[{TAG}] 전송 스레드 종료...");
                mv_sendRunning = false;

                // 전송 중지 메시지 생성
                MessageStop messageStop = new MessageStop
                {
                    RecKey = mv_recKey,
                    SubKey = mv_subKey,
                    FileSize = mv_fileSize
                };

                if (mv_recServer != null) mv_recServer.SendMessage(messageStop);
                if (mv_recServer_2 != null) mv_recServer_2.SendMessage(messageStop);

                // 채널분리 패킷 전송
                if (mv_isSubRecorder)
                {
                    if (mv_recServer_sub != null) mv_recServer_sub.SendMessage(messageStop);
                    if (mv_recServer_2_sub != null) mv_recServer_2_sub.SendMessage(messageStop);
                }

                mv_recKey = "";
                mv_subKey = "";
                mv_fileSize = 0;

                Thread.Sleep(500);

                if (mv_recServer != null)
                {
                    mv_recServer.Stop();
                    mv_recServer = null;
                }

                if (mv_recServer_2 != null)
                {
                    mv_recServer_2.Stop();
                    mv_recServer_2 = null;
                }

                if (mv_recServer_sub != null)
                {
                    mv_recServer_sub.Stop();
                    mv_recServer_sub = null;
                }

                if (mv_recServer_2_sub != null)
                {
                    mv_recServer_2_sub.Stop();
                    mv_recServer_2_sub = null;
                }

                if (m_sendThread != null)
                {
                    try
                    {
                        m_sendThread.Join();
                        Logger.Debug($"[{TAG}] 전송 스레드 종료!");
                    }
                    catch (ThreadInterruptedException e)
                    {
                        Logger.Error($"[{TAG}] 전송 스레드 중지 중 오류 발생: {e.Message}");
                    }
                    finally
                    {
                        m_sendThread = null;
                    }
                }
                else
                {
                    Logger.Info($"[{TAG}] 이미 종료 된 스레드입니다.");
                }
            }
            return true;
        }


        /*
        * @Description 패킷 전송 스레드
        */
        private class SendThread
        {
            private readonly AudioManager _audioManager;

            public SendThread(AudioManager audioManager)
            {
                _audioManager = audioManager;
            }

            public void Run()
            {
                Logger.Debug($"[{TAG}] 전송 스레드 시작!");
                string key = $"{_audioManager.mv_recKey}_{_audioManager.mv_subKey}";

                var userId = "";

                IniFile _ini = new IniFile(Path.Combine(AppContext.BaseDirectory, "config.ini"));
                var clientSetting = _ini.Get("Client", "Web");

                try
                {
                    if( clientSetting == "true" )
                    {
                        if (_audioManager.m_voiceManager != null) userId = _audioManager.m_voiceManager.GetUserId();
                    }
                    else
                    {
                        var userInfo = LoginConfigService.Load();
                        if (userInfo != null) userId = userInfo.UserId;
                    }
                }
                catch (Exception)
                { };


                var messageStart = new MessageStart
                {
                    RecKey = _audioManager.mv_recKey,
                    SubKey = _audioManager.mv_subKey,
                    FileFormat = _audioManager.mv_fileFormat,
                    RecordType = 1,
                    EquipNo = 1,
                    Channel = _audioManager.mv_audioChannel,
                    SampleRate = _audioManager.mv_sampleRate,
                    BitPerSample = 16,
                    UserId = userId
                };

                bool ret1 = false;
                bool ret2 = false;

                // 메인 서버 패킷 전달
                if (_audioManager.mv_recServer != null)
                {
                    _audioManager.mv_recServer.Start();

                    ret1 = _audioManager.mv_recServer.SendMessage(messageStart);

                    if (ret1) Logger.Info($"[{TAG}] 1번 서버 패킷 전송 시작!");

                    // 채널분리 패킷 전송
                    if (_audioManager.mv_isSubRecorder)
                    {
                        messageStart = new MessageStart
                        {
                            RecKey = _audioManager.mv_recKey,
                            SubKey = _audioManager.mv_subKey,
                            FileFormat = _audioManager.mv_fileFormat,
                            RecordType = 1,
                            EquipNo = 2,
                            Channel = _audioManager.mv_audioChannel,
                            SampleRate = _audioManager.mv_sampleRate,
                            BitPerSample = 16,
                            UserId = userId
                        };

                        if (_audioManager.mv_recServer_sub != null)
                        {
                            _audioManager.mv_recServer_sub.Start();

                            Logger.Info($"[{TAG}] 1번 서버 보조소켓 패킷 전송 시작!");

                            _audioManager.mv_recServer_sub.SendMessage(messageStart);
                        }
                    }
                }

                // 이중화 서버 패킷 전달
                if (_audioManager.mv_recServer_2 != null)
                {
                    _audioManager.mv_recServer_2.Start();

                    ret2 = _audioManager.mv_recServer_2.SendMessage(messageStart);

                    if (ret2) Logger.Info($"[{TAG}] 2번 서버 패킷 전송 시작!");

                    // 채널분리 패킷 전송
                    if (_audioManager.mv_isSubRecorder)
                    {
                        messageStart = new MessageStart
                        {
                            RecKey = _audioManager.mv_recKey,
                            SubKey = _audioManager.mv_subKey,
                            FileFormat = _audioManager.mv_fileFormat,
                            RecordType = 1,
                            EquipNo = 2,
                            Channel = _audioManager.mv_audioChannel,
                            SampleRate = _audioManager.mv_sampleRate,
                            BitPerSample = 16,
                            UserId = userId
                        };

                        if (_audioManager.mv_recServer_2_sub != null)
                        {
                            _audioManager.mv_recServer_2_sub.Start();

                            Logger.Info($"[{TAG}] 2번 서버 보조소켓 패킷 전송 시작!");

                            _audioManager.mv_recServer_2_sub.SendMessage(messageStart);
                        }
                    }
                }

                // 연결되는 서버가 존재하면 패킷 전달
                if (ret1 || ret2)
                {
                    _audioManager.mv_sendRunning = true;
                    _audioManager.sendWorker();

                    if (_audioManager.mv_isSubRecorder) _audioManager.sendWorkerSub();
                }
            }
        }

        /*
        * @Description 패킷 전달 워커
        */
        private Task sendWorker()
        {
            return Task.Run(() =>
            {
                Logger.Debug($"[{TAG}] 패킷 워커 시작!");

                LinkedList<byte[]> buffer = m_outBuffer[0];

                while (true)
                {
                    if (!mv_sendRunning) break;

                    bool disConn = false;
                    // 1번 서버 커넥션이 끊겼을 경우  
                    if (mv_recServer != null && !mv_recServer.isConnected)
                        disConn = true;

                    // 1번 서버 커넥션이 끊겼지만, 2번 서버가 연결된 경우  
                    if (!disConn && mv_recServer_2 != null && mv_recServer_2.isConnected)
                        disConn = false;

                    if (disConn)
                    {
                        Logger.Error($"[{TAG}] 녹취 서버 연결 끊김...");
                        if (m_voiceManager != null) m_voiceManager.AudioClose();

                        Logger.Debug($"[{TAG}] 패킷 워커 종료!");

                        break;
                    }



                    int sendSize = GetDataSize(buffer);
                    if (sendSize > 0)
                    {
                        MessageSend messageSend = new MessageSend
                        {
                            RecKey = mv_recKey,
                            SubKey = mv_subKey,
                            Data = GetData(buffer)
                        };

                        if (mv_recServer != null) mv_recServer.SendMessage(messageSend);
                        if (mv_recServer_2 != null) mv_recServer_2.SendMessage(messageSend);
                    }
                }
            });
        }

        private Task sendWorkerSub()
        {
            return Task.Run(() =>
            {
                Logger.Debug($"[{TAG}] 보조 패킷 워커 시작!");

                LinkedList<byte[]> buffer_sub = m_outBuffer[1];

                while (true)
                {
                    if (!mv_sendRunning) break;

                    if (buffer_sub != null)
                    {
                        int sendSize_sub = GetDataSize(buffer_sub);
                        if (sendSize_sub > 0)
                        {
                            MessageSend messageSend = new MessageSend
                            {
                                RecKey = mv_recKey,
                                SubKey = mv_subKey,
                                Data = GetData(buffer_sub)
                            };

                            if (mv_recServer_sub != null) mv_recServer_sub.SendMessage(messageSend);
                            if (mv_recServer_2_sub != null) mv_recServer_2_sub.SendMessage(messageSend);
                        }
                    }
                }

                Logger.Debug($"[{TAG}] 보조 패킷 워커 종료!");
            });
        }

        public void SetMute(int param)
        {
            mv_mute = param;
        }

        public bool SetRecordKey(string recKey, string subKey)
        {
            mv_recKey = recKey;
            mv_subKey = subKey;
            return true;
        }

        public bool SetOutputFile(string filePath, string fileName)
        {
            mv_fileName = fileName;
            mv_filePath = filePath;
            return true;
        }

        public void SetEngineUrl(int no, string url, int port)
        {
            switch (no)
            {
                case 1:
                    mv_engineUrl = url;
                    mv_enginePort = port;
                    break;
                case 2:
                    mv_engineUrl_2 = url;
                    mv_enginePort_2 = port;
                    break;
            }
            Logger.Info($"[{TAG}]IP / PORT 설정 - {no} / {url} / {port}");
        }

        private void Wait(int sec)
        {
            Thread.Sleep(sec * 1000);
            return;
        }

        // 현재 녹음된 파일 크기 반환
        public byte[] GetData(LinkedList<byte[]> buffer)
        {
            if (buffer != null && buffer.Count > 0)
            {
                lock (buffer)
                {
                    if (buffer.First != null)
                    {
                        byte[] data = buffer.First.Value; // 첫 번째 데이터 가져오기
                        buffer.RemoveFirst(); // 첫 번째 데이터 제거
                        mv_fileSize += data.Length;
                        return data;
                    }
                }
            }
            return [0];
        }

        public int GetDataSize(LinkedList<byte[]> buffer)
        {
            if (buffer != null)
            {
                try
                {
                    int bufferSize = buffer.Count;
                    if (bufferSize > 0)
                    {
                        return bufferSize;
                    }
                }
                catch (Exception)
                {
                    // 예외 발생 시 로깅 가능
                }
            }
            return 0;
        }
        public bool GetPaused()
        {
            return mv_audioPaused;
        }

        public int GetMute()
        {
            return mv_mute;
        }

    }
}