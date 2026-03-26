using System.Globalization;
using System.Windows.Data;

namespace RecordClient.Helpers.Converters
{
    public class BoolInverseConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool b)
                return !b;
            return true; // 기본값 true (사용 가능)
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }
}
