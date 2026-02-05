using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace LemiCraft_Launcher.Utils
{
    public class TextEmptyToVisibility : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            if (values.Length == 0) return Visibility.Visible;
            var s = values[0] as string;
            return string.IsNullOrEmpty(s) ? Visibility.Visible : Visibility.Collapsed;
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}