using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Data;

namespace PixelBridge
{
    internal class BooleanToEnable_SendDataInTextBox_Converter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            if(value is SendDataMode v)
            {
                if(v == SendDataMode.StringInTextBox) return true;
                else return false;
            }
            return false;
        }
        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            if (value is SendDataMode v)
            {
                if (v == SendDataMode.StringInTextBox) return false;
                else return true;
            }
            return true;
        }
    }

    internal class BooleanToEnable_FixedData_Converter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            if (value is SendDataMode v)
            {
                if (v == SendDataMode.FixedString) return true;
                else return false;
            }
            return false;
        }
        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            if (value is SendDataMode v)
            {
                if (v == SendDataMode.FixedString) return false;
                else return true;
            }
            return true;
        }
    }
}
