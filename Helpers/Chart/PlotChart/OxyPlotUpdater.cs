using OxyPlot;
using OxyPlot.Series;

namespace RecordClient.Helpers.Chart.PlotChart
{
    public static class OxyPlotUpdater
    {
        /// <summary>
        /// 좌/우 채널의 실시간 피크 데이터를 AreaSeries에 추가하고,
        /// 스크롤 및 최대 표시 갯수를 유지하면서 PlotModel을 갱신한다.
        /// </summary>
        public static void UpdatePeakPlot(
            double peakLeft,               // 좌측 채널 피크값
            double peakRight,              // 우측 채널 피크값
            ref int sampleIndex,           // 샘플 인덱스 (시간축 증가용)
            AreaSeries leftArea,           // 좌측 채널 AreaSeries (Points / Points2)
            AreaSeries rightArea,          // 우측 채널 AreaSeries (Points / Points2)
            PlotModel model,               // 갱신할 OxyPlot 모델
            double scrollFactor,           // 시간축 스크롤 속도 배율
            int waveDisplayLimit = 100     // 화면에 보여줄 최대 샘플 개수
        )
        {
            // NaN 데이터는 무시 (정상 측정이 아닐 경우)
            if (float.IsNaN((float)peakLeft) || float.IsNaN((float)peakRight)) return;

            // 시간 샘플 증가
            sampleIndex++;

            // 1. 좌/우 파형 + 기준선(0) 데이터 추가
            leftArea.Points.Add(new DataPoint(sampleIndex, peakLeft));
            leftArea.Points2.Add(new DataPoint(sampleIndex, 0)); // 기준선

            rightArea.Points.Add(new DataPoint(sampleIndex, peakRight));
            rightArea.Points2.Add(new DataPoint(sampleIndex, 0)); // 기준선

            // 2. X축 스크롤 처리 (최신 샘플 기준으로 이동)
            model.Axes[1].Minimum = sampleIndex * scrollFactor - waveDisplayLimit;
            model.Axes[1].Maximum = sampleIndex * scrollFactor;

            // 3. 최대 갯수 초과 시 오래된 데이터 제거 (Points & Points2 동시)
            if (leftArea.Points.Count > waveDisplayLimit)
            {
                leftArea.Points.RemoveAt(0);
                leftArea.Points2.RemoveAt(0);
            }
            if (rightArea.Points.Count > waveDisplayLimit)
            {
                rightArea.Points.RemoveAt(0);
                rightArea.Points2.RemoveAt(0);
            }

            // 4. Plot 갱신
            model.InvalidatePlot(false);
        }
    }
}
