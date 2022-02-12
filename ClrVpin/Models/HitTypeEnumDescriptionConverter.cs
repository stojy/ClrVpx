using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using Utils;
using Utils.Extensions;

namespace ClrVpin.Models
{
    public class HitTypeEnumDescriptionConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return value is HitTypeEnum hitType ? hitType.GetDescription() : DependencyProperty.UnsetValue;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}