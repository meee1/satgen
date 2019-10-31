using Racelogic.WPF.Controls;
using System;
using System.CodeDom.Compiler;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Shapes;

namespace Racelogic.Gnss.SatGen.BlackBox
{
	public partial class RealTimeView : UserControl, IComponentConnector //, IStyleConnector
	{
		private readonly SimulationViewModel viewModel;

		private const int minSatCount = 4;

		private const double sliderWidth = 46.0;

		private const double defaultHeight = 300.0;

	

		public RealTimeView(SimulationViewModel viewModel)
		{
			InitializeComponent();
			this.viewModel = viewModel;
		}

		private async void OnCancelButtonClick(object sender, RoutedEventArgs e)
		{
			Thread.CurrentThread.Priority = ThreadPriority.Highest;
			CancelButton.Content = "Wait";
			base.IsEnabled = false;
			await Task.Run(delegate
			{
				viewModel.Simulation.Cancel();
			});
			Thread.CurrentThread.Priority = ThreadPriority.Normal;
		}

		private void OnSliderDoubleClick(object sender, MouseButtonEventArgs e)
		{
			if (e.OriginalSource is Path)
			{
				Slider slider = sender as Slider;
				if (slider != null)
				{
					slider.Value = slider.Maximum;
				}
			}
		}

		protected override Size MeasureOverride(Size constraint)
		{
			if (constraint.Width == double.PositiveInfinity && constraint.Height == double.PositiveInfinity)
			{
				int num = (from c in viewModel.VisibleSatellites
				select c.Satellites.Count).Max();
				if (num < 4)
				{
					num = 4;
				}
				return new Size((double)num * 46.0, 300.0);
			}
			return base.MeasureOverride(constraint);
		}
        
	}
}
