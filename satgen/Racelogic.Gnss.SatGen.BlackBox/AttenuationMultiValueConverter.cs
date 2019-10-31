using Racelogic.WPF.Utilities;
using System;
using System.Globalization;
using System.Linq;
using System.Windows.Data;

namespace Racelogic.Gnss.SatGen.BlackBox
{
	public class AttenuationMultiValueConverter : IMultiValueConverter
	{
		public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
		{
			double valueOrDefault = BindingHelper.ConvertToDouble(values.FirstOrDefault()).GetValueOrDefault();
			double num = BindingHelper.ConvertToDouble(values.ElementAtOrDefault(1)) ?? (-30.0);
			if (valueOrDefault < num)
			{
				return num;
			}
			return valueOrDefault;
		}

		public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
		{
			return new object[1]
			{
				value
			};
		}
	}
}
