using NAudio.Wave;

namespace RecordClient.Services.Audio
{
    /// <summary>
    /// 오디오 입력 데이터를 처리하여 정규화된 좌/우 채널 버퍼를 생성하고,
    /// DC 오프셋, 피크 추적 등 전처리 상태를 유지 관리하는 클래스
    /// 
    /*readonly SoundProcessingService _soundProcess = new( // 디폴트 세팅값 (0.7f, 0.8f, 0.97, 4.0, 0.6);
        inputGain: 0.7f,    // 입력 증폭 (마이크 감도)
        peakSmooth: 0.8f,   // 피크 LERP 보정 계수
        dcSmoothing: 0.97f, // DC 오프셋 보정 계수
        amplifyFactor: 4.0, // 출력 파형 증폭 비율
        maxAmplitude: 0.6   // 출력 파형 최대 진폭 제한
    );
*/
    /// </summary>
    public class SoundProcessingService
    {
        // 외부에서 읽기용으로 접근 가능한 전처리 상태값
        public float DCMovingAvg => _dcMovingAvg;   // DC 오프셋 누적 평균값
        public float PrevLeft => _prevLeft;         // 이전 프레임 좌 채널 피크값
        public float PrevRight => _prevRight;       // 이전 프레임 우 채널 피크값

        // 내부 상태 필드
        private float _dcMovingAvg = 0;
        private float _prevLeft = 0;
        private float _prevRight = 0;

        // 오디오 전처리 설정값
        private readonly float _inputGain;          // 입력 증폭 (마이크 감도)
        private readonly float _peakSmooth;         // 피크 LERP 보정 계수
        private readonly float _dcSmoothing;        // DC 오프셋 보정 계수
        private readonly double _amplifyFactor;     // 출력 파형 증폭 비율
        private readonly double _maxAmplitude;      // 출력 파형 최대 진폭 제한

        /// <summary>
        /// 오디오 전처리 파라미터를 초기화합니다.
        /// </summary>
        public SoundProcessingService(
            float inputGain,
            float peakSmooth,
            float dcSmoothing,
            double amplifyFactor,
            double maxAmplitude)
        {
            _inputGain = inputGain;
            _peakSmooth = peakSmooth;
            _dcSmoothing = dcSmoothing;
            _amplifyFactor = amplifyFactor;
            _maxAmplitude = maxAmplitude;
        }

        /// <summary>
        /// 마이크 입력 버퍼를 전처리하여 좌/우 채널 버퍼에 저장합니다.
        /// 내부적으로 SoftClip, DC 제거, 증폭, 클리핑, 오프셋 보정 등이 수행됩니다.
        /// </summary>
        public void Process(WaveInEventArgs e, List<double> leftBuffer, List<double> rightBuffer)
        {
            SoundProcessor.ProcessSoundBuffer(
                e,
                _inputGain,
                _peakSmooth,
                _amplifyFactor,
                _maxAmplitude,
                _dcSmoothing,
                ref _dcMovingAvg,
                ref _prevLeft,
                ref _prevRight,
                leftBuffer,
                rightBuffer
            );
        }
    }
}
