using OxyPlot;
using OxyPlot.Axes;
using OxyPlot.Series;

public static class OxyPlotInitializer
{
    /// <summary>
    /// OxyPlot 기반 파형 차트를 초기화합니다.
    /// 좌/우 채널용 AreaSeries와 PlotModel을 구성하며 초기 샘플 인덱스를 설정합니다.
    /// </summary>
    /// <param name="leftAreaSeries">좌 채널 시리즈</param>
    /// <param name="rightAreaSeries">우 채널 시리즈</param>
    /// <param name="sampleIndex">초기 샘플 인덱스 (기본값 100)</param>
    /// <returns>초기화된 PlotModel 객체</returns>
    public static PlotModel InitializeOxyPlot(
        out AreaSeries leftAreaSeries,
        out AreaSeries rightAreaSeries,
        out int sampleIndex)
    {
        // OxyPlot의 전반적인 스타일 및 배경 설정
        var model = new PlotModel
        {
            Background = OxyColor.FromRgb(10, 10, 20),
            PlotMargins = new OxyThickness(0),
            PlotAreaBorderThickness = new OxyThickness(0),
            PlotAreaBackground = OxyColors.Transparent,
            IsLegendVisible = false,
        };

        // 좌측 채널 시리즈 구성
        leftAreaSeries = new AreaSeries
        {
            Title = "Left",
            Color = OxyColors.DeepSkyBlue,
            Fill = OxyColor.FromAColor(255, OxyColors.DeepSkyBlue),
            StrokeThickness = 2,
            InterpolationAlgorithm = InterpolationAlgorithms.CanonicalSpline,
            MarkerType = MarkerType.None,
            MarkerFill = OxyColors.Transparent,
            MarkerStroke = OxyColors.Transparent,
            MarkerStrokeThickness = 0,
            CanTrackerInterpolatePoints = false,
        };

        // 우측 채널 시리즈 구성
        rightAreaSeries = new AreaSeries
        {
            Title = "Right",
            Color = OxyColors.OrangeRed,
            Fill = OxyColor.FromAColor(255, OxyColors.OrangeRed),
            StrokeThickness = 2,
            InterpolationAlgorithm = InterpolationAlgorithms.CanonicalSpline,
        };

        // 좌/우 채널 모두에 초기 데이터 (0값 100개) 삽입
        for (int i = 0; i < 100; i++)
        {
            leftAreaSeries.Points.Add(new DataPoint(i, 0));
            leftAreaSeries.Points2.Add(new DataPoint(i, 0));

            rightAreaSeries.Points.Add(new DataPoint(i, 0));
            rightAreaSeries.Points2.Add(new DataPoint(i, 0));
        }

        sampleIndex = 100;

        model.Series.Add(leftAreaSeries);
        model.Series.Add(rightAreaSeries);

        // Y축 설정 (좌측)
        model.Axes.Add(new LinearAxis
        {
            Position = AxisPosition.Left,
            IsAxisVisible = false,
            Minimum = -0.34,
            Maximum = 0.34,
            AbsoluteMinimum = -0.4,
            AbsoluteMaximum = 0.4,
            IsZoomEnabled = false,
            IsPanEnabled = false,
            MajorGridlineStyle = LineStyle.None,
            MinorGridlineStyle = LineStyle.None,
            AxislineStyle = LineStyle.None,
        });

        // X축 설정 (하단, 스크롤 위치 기준)
        model.Axes.Add(new LinearAxis
        {
            Position = AxisPosition.Bottom,
            IsAxisVisible = false,
            IsZoomEnabled = false,
            IsPanEnabled = false,
            MajorGridlineStyle = LineStyle.None,
            MinorGridlineStyle = LineStyle.None,
            AxislineStyle = LineStyle.None,
            AxislineColor = OxyColors.Transparent,
            PositionTier = 1,
            Layer = AxisLayer.BelowSeries,
        });

        return model;
    }
}