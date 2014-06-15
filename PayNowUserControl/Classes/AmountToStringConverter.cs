using System;
using System.Globalization;
using System.Windows.Data;

namespace PayNowUserControl
{
    public class AmountToStringConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var amount = (long)value;

            var doubleAmount = ((double)amount) / 100;
            return doubleAmount.ToString(CultureInfo.InvariantCulture);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var doubleAmount = System.Convert.ToDouble((string) value);

            var amount = (long)(doubleAmount * 100);
            return amount;
        }
    }
}