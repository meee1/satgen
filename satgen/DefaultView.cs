using System;
using System.CodeDom.Compiler;
using System.ComponentModel;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;

namespace Racelogic.Gnss.SatGen.BlackBox
{
	public partial class DefaultView : UserControl, IComponentConnector
	{
		private readonly SimulationViewModel viewModel;

		private static readonly Size defaultSize = new Size(420.0, 100.0);


		public DefaultView(SimulationViewModel viewModel)
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

		protected override Size MeasureOverride(Size constraint)
		{
			if (constraint.Width == double.PositiveInfinity && constraint.Height == double.PositiveInfinity)
			{
				return defaultSize;
			}
			return base.MeasureOverride(constraint);
		}
        
	}
}
