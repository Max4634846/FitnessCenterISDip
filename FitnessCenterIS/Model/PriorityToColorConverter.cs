using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Data;
using System.Windows.Media;

namespace FitnessCenterIS.Model
{
    public class PriorityToColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is string priority)
            {
                switch (priority.ToLower())
                {
                    case "высокий":
                        return new SolidColorBrush(Colors.Red);
                    case "средний":
                        return new SolidColorBrush(Colors.Orange);
                    case "низкий":
                        return new SolidColorBrush(Colors.Green);
                    default:
                        return new SolidColorBrush(Colors.Gray); // Цвет по умолчанию
                }
            }
            return new SolidColorBrush(Colors.Gray); // Цвет по умолчанию, если значение не строка
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
