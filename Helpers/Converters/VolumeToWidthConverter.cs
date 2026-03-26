using System.Diagnostics;
using System.Globalization;
using System.Windows.Data;

namespace RecordClient.Helpers.Converters
{
    public class VolumeToWidthConverter : IValueConverter
    {
        public double MaxWidth { get; set; } = 150; // 슬라이더 너비 기준
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            Debug.WriteLine($"[VolumeToWidthConverter] value: {value}, type: {value?.GetType().Name}");


            if (value is float f)
                return f * MaxWidth;
            else if (value is double d)
                return d * MaxWidth;

            return 0.0;

            /*Debug.WriteLine("CALL");
            return 100.0; // 무조건 100 반환*/
        }


        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}