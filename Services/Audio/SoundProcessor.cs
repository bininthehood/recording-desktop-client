using NAudio.Wave;
namespace RecordClient.Services.Audio
{
    public static class SoundProcessor
    {
        /// <summary>
        /// 마이크로부터 수신된 PCM 오디오 데이터를 전처리하여 좌/우 채널별로 정규화된 샘플 리스트에 저장한다.
        /// </summary>
        public static void ProcessSoundBuffer(
            WaveInEventArgs e,
            float inputGain,              // 입력 증폭 계수 (마이크 감도 조절)
            float peakSmooth,             // 피크 보정용 지수 가중치 (LERP)
            double amplifyFactor,         // 최종 출력 파형 확대 계수
            double maxAmplitudeLimit,     // 최대 진폭 제한값 (클리핑 방지용)
            float dcSmoothing,            // DC 오프셋 평균 보정 계수
            ref float dcMovingAvg,        // DC 오프셋 누적 평균값 (프레임 간 유지)
            ref float prevLeft,           // 좌 채널 피크 추적값 ( OxyPlot 전용 )
            ref float prevRight,          // 우 채널 피크 추적값 ( OxyPlot 전용 )
            List<double> newLeftBuffer,   // 정규화된 좌측 채널 버퍼
            List<double> newRightBuffer   // 정규화된 우측 채널 버퍼
        )
        {
            newLeftBuffer.Clear();
            newRightBuffer.Clear();

            // 16비트 스테레오 PCM 처리: 4바이트 단위 (좌 2바이트 + 우 2바이트)
            for (int index = 0; index < e.BytesRecorded; index += 4)
            {
                // 1. 바이트 → 16비트 샘플 추출
                short leftSample = (short)(e.Buffer[index] | e.Buffer[index + 1] << 8);
                short rightSample = (short)(e.Buffer[index + 2] | e.Buffer[index + 3] << 8);

                // 2. -1.0 ~ 1.0 범위로 정규화 후 입력 감도 반영
                float left = leftSample / 32768f * inputGain;
                float right = rightSample / 32768f * inputGain;

                // 3. DC 오프셋 누적 평균 보정
                float rawOffset = (left + right) / 2f;
                dcMovingAvg = dcMovingAvg * dcSmoothing + rawOffset * (1 - dcSmoothing);
                left -= dcMovingAvg;
                right -= dcMovingAvg;

                // 4. 소프트 클리핑 (디스토션 방지용 비선형 처리)
                left = SoftClipAtan(left);
                right = SoftClipAtan(right);

                // 5. 이전 피크와 선형 보간하여 부드러운 피크 추적
                prevLeft = prevLeft * peakSmooth + left * (1 - peakSmooth);
                prevRight = prevRight * peakSmooth + right * (1 - peakSmooth);

                // 6. 출력용 증폭 처리
                double normalizedLeft = left * amplifyFactor;
                double normalizedRight = right * amplifyFactor;

                // 7. 최대 진폭 제한 (클리핑 방지)
                normalizedLeft = Math.Clamp(normalizedLeft, -maxAmplitudeLimit, maxAmplitudeLimit);
                normalizedRight = Math.Clamp(normalizedRight, -maxAmplitudeLimit, maxAmplitudeLimit);

                // 8. DC 기준값 재보정 (좌우 평균 제거)
                double offset = (normalizedLeft + normalizedRight) / 2.0;
                normalizedLeft -= offset;
                normalizedRight -= offset;

                // 9. 최종 파형 버퍼에 저장
                newLeftBuffer.Add(normalizedLeft);
                newRightBuffer.Add(normalizedRight);
            }
        }

        /// <summary>
        /// Soft clipping 함수 (tanh, atan 등 사용 가능) - 여기선 atan 기반
        /// </summary>
        private static float SoftClipAtan(float x)
        {
            return (float)(Math.Atan(x) / (Math.PI / 2));
        }
    }
}
