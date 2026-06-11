using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace Smart_Home.Helpers
{
    public class BoolToColorConverter : IValueConverter
    {
        public SolidColorBrush TrueColor { get; set; } = new SolidColorBrush(Colors.LimeGreen);
        public SolidColorBrush FalseColor { get; set; } = new SolidColorBrush(Colors.Red);

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool b && b)
                return TrueColor;
            return FalseColor;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return Binding.DoNothing;
        }
    }
}
