using LiveChartsCore;
using LiveChartsCore.SkiaSharpView;

namespace RecordClient.Helpers.Chart.WaveChart
{
    public static class WaveChartUpdater
    {
        public static void UpdateWaveChart(
            List<double> leftData,
            List<double> rightData,
            List<double> newLeftBuffer,
            List<double> newRightBuffer,
            List<Axis> yAxis,
            ISeries leftSeries,
            ISeries rightSeries,
            int maxCount = 150)
        {
            // 새 데이터 추가
            leftData.AddRange(newLeftBuffer);
            rightData.AddRange(newRightBuffer);

            // 최근 n개 데이터만 유지 :  X축 표시 갯수 - n 클 수록 가로로 길게 퍼짐
            if (leftData.Count > maxCount)
                leftData = leftData.Skip(leftData.Count - maxCount).ToList();

            if (rightData.Count > maxCount)
                rightData = rightData.Skip(rightData.Count - maxCount).ToList();

            // Y축 스케일 고정 또는 부드럽게
            double maxAmplitude = Math.Max(
                leftData.DefaultIfEmpty(0).Max(Math.Abs),
                rightData.DefaultIfEmpty(0).Max(Math.Abs)
            );

            double safeMax = Math.Max(0.05, maxAmplitude); // 최소 0.05 유지 (너무 좁아지지 않게)
            double range = safeMax * 1.2;

            // Y축 고정값 설정 ( range 사용 가능 )
            yAxis[0].MinLimit = -1;
            yAxis[0].MaxLimit = 1;

            // 시리즈에 새로운 값 반영
            leftSeries.Values = leftData;
            rightSeries.Values = rightData;
        }
    }
}
