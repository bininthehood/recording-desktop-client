using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.WPF;
using RecordClient.Models.Chart;

public static class WaveChartInitializer
{
    /// <summary>
    /// LiveCharts 기반 웨이브 차트를 초기화합니다.
    /// 좌/우 채널 LineSeries와 축 설정, 툴팁 위치를 지정합니다.
    /// </summary>
    public static void InitializeWaveChart(
        CartesianChart chart,
        List<double> leftData,
        List<double> rightData,
        out LineSeries<double> leftSeries,
        out LineSeries<double> rightSeries,
        out List<Axis> xAxes,
        out List<Axis> yAxes)
    {
        // X축: 라벨 숨김 + 최소 간격 지정
        xAxes =
        [
            new Axis
            {
                IsVisible = false,
                MinStep = 1
            }
        ];

        // Y축: -1 ~ 1 고정, 라벨 숨김
        yAxes =
        [
            new Axis
            {
                MinLimit = -1,
                MaxLimit = 1,
                IsVisible = false
            }
        ];

        // 시리즈 생성
        var (left, right) = ChartStyleFactory.CreateWaveSeriesPair(
            WaveTheme.Rainbow, // Rainbow 테마
            leftData,
            rightData
        );

        leftSeries = left;
        rightSeries = right;

        // 차트 구성 요소 적용
        chart.Series = new[] { leftSeries, rightSeries };
        chart.XAxes = xAxes;
        chart.YAxes = yAxes;
        chart.TooltipPosition = LiveChartsCore.Measure.TooltipPosition.Hidden;
    }
}