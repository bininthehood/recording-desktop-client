using LiveChartsCore.Painting;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using SkiaSharp;

namespace RecordClient.Models.Chart
{
    public enum WaveTheme
    {
        CoolBlue,
        WarmGold,
        Rainbow
    }

    public static class ChartStyleFactory
    {
        public static (LineSeries<double> left, LineSeries<double> right) CreateWaveSeriesPair(
            WaveTheme theme,
            List<double> leftData,
            List<double> rightData)
        {
            var (leftStroke, leftFill, rightStroke, rightFill) = GetThemePaints(theme);

            var leftSeries = CreateWaveSeries(leftData, leftStroke, leftFill);
            var rightSeries = CreateWaveSeries(rightData, rightStroke, rightFill);

            return (leftSeries, rightSeries);
        }

        public static LineSeries<double> CreateWaveSeries(
            List<double> values,
            Paint stroke,
            Paint? fill = null)
        {
            return new LineSeries<double>
            {
                Values = values,
                Stroke = stroke,
                Fill = fill,
                LineSmoothness = 0.7,
                GeometrySize = 0
            };
        }

        public static LinearGradientPaint CreateGradientPaint(
            SKColor[] colors,
            SKPoint start,
            SKPoint end,
            float[] positions)
        {
            return new LinearGradientPaint(colors, start, end, positions, SKShaderTileMode.Clamp);
        }

        private static (Paint leftStroke, Paint? leftFill, Paint rightStroke, Paint? rightFill) GetThemePaints(WaveTheme theme)
        {
            switch (theme)
            {
                case WaveTheme.CoolBlue:
                    return (
                        CreateGradientPaint(new[] { SKColors.DeepSkyBlue, SKColors.Cyan }, new SKPoint(0, 0), new SKPoint(1, 0), new[] { 0f, 1f }),
                        CreateGradientPaint(new[] { new SKColor(0, 255, 255, 60), new SKColor(0, 255, 255, 0) }, new SKPoint(0, 0), new SKPoint(0, 1), new[] { 0f, 1f }),
                        CreateGradientPaint(new[] { SKColors.LightBlue, SKColors.Blue }, new SKPoint(0, 0), new SKPoint(1, 0), new[] { 0f, 1f }),
                        CreateGradientPaint(new[] { new SKColor(0, 128, 255, 50), new SKColor(0, 128, 255, 0) }, new SKPoint(0, 0), new SKPoint(0, 1), new[] { 0f, 1f })
                    );

                case WaveTheme.WarmGold:
                    return (
                        CreateGradientPaint(new[] { SKColors.Orange, SKColors.Gold }, new SKPoint(0, 0), new SKPoint(1, 0), new[] { 0f, 1f }),
                        CreateGradientPaint(new[] { new SKColor(255, 165, 0, 50), new SKColor(255, 215, 0, 0) }, new SKPoint(0, 0), new SKPoint(0, 1), new[] { 0f, 1f }),
                        CreateGradientPaint(new[] { SKColors.Red, SKColors.OrangeRed }, new SKPoint(0, 0), new SKPoint(1, 0), new[] { 0f, 1f }),
                        CreateGradientPaint(new[] { new SKColor(255, 69, 0, 50), new SKColor(255, 140, 0, 0) }, new SKPoint(0, 0), new SKPoint(0, 1), new[] { 0f, 1f })
                    );

                case WaveTheme.Rainbow:
                    return (
                        CreateGradientPaint(new[] {
                            SKColors.Red, SKColors.Orange, SKColors.Yellow,
                            SKColors.Green, SKColors.Blue, SKColors.Indigo, SKColors.Violet
                        }, new SKPoint(0, 0), new SKPoint(1, 0),
                        new[] { 0f, 0.17f, 0.33f, 0.5f, 0.67f, 0.83f, 1f }),
                        null,
                        CreateGradientPaint(new[] {
                            SKColors.Violet, SKColors.Indigo, SKColors.Blue,
                            SKColors.Green, SKColors.Yellow, SKColors.Orange, SKColors.Red
                        }, new SKPoint(0, 0), new SKPoint(1, 0),
                        new[] { 0f, 0.17f, 0.33f, 0.5f, 0.67f, 0.83f, 1f }),
                        null
                    );

                default:
                    throw new ArgumentOutOfRangeException(nameof(theme));
            }
        }
    }
}
