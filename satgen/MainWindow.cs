using Racelogic.DataTypes;
using Racelogic.Geodetics;
using Racelogic.Gnss.LabSat;
using Racelogic.Gnss.SatGen.BlackBox.Properties;
using Racelogic.Utilities;
using Racelogic.WPF.Utilities;
using System;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Threading;

namespace Racelogic.Gnss.SatGen.BlackBox
{
	public partial class MainWindow : Window, IDisposable, IComponentConnector
	{
		private readonly SimulationViewModel viewModel;

		private const double trueTimeStartDelay = 10.0;

		private const double defaultHorizontalMargin = 20.0;

		private const double defaultVerticalMargin = 38.0;

		private const double clientAreaMargin = 2.0;

		private const int ticksPerSecond = 10000000;

		private readonly Size preferredViewSize;

		private readonly Simulation simulation;

		private readonly ILiveOutput liveOutput;

		private readonly SemaphoreSlim taskbarItemSemaphore = new SemaphoreSlim(1);

		private const int highChippingRateThreshold = 5000000;

		private bool isDisposed;

	



		public MainWindow()
		{
			InitializeComponent();
			AppDomain.CurrentDomain.UnhandledException += OnCurrentDomainUnhandledException;
			TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;
			Application.Current.SessionEnding += OnSessionEnding;
			string[] commandLineArgs = Environment.GetCommandLineArgs();
			if (commandLineArgs.Length != 2)
			{
				Environment.Exit(-1);
			}
			if (!File.Exists(commandLineArgs[1]))
			{
				Environment.Exit(-1);
			}
			ConfigFile config = ConfigFile.Read(commandLineArgs[1]);
			TrajectorySource trajectorySource = Key.LeftShift.IsDownAsync() ? TrajectorySource.Joystick : TrajectorySource.NmeaFile;
			Output output = null;
			try
			{
				output = GetOutput(config);
			}
			catch (LabSatException ex)
			{
				MessageBox.Show(this, ex.Message, "Error");
				Application.Current.Shutdown();
				return;
			}
			output.Error += OnOutputError;
			liveOutput = (output as ILiveOutput);
			if (liveOutput != null)
			{
				Process.GetCurrentProcess().PriorityClass = ProcessPriorityClass.RealTime;
			}
			simulation = GetSimulator(config, output, trajectorySource);
			if (simulation == null)
			{
				Environment.Exit(-1);
				return;
			}
			viewModel = new SimulationViewModel(simulation);
			base.DataContext = viewModel;
			Control control = (Control)((liveOutput == null) ? ((object)new DefaultView(viewModel)) : ((object)new RealTimeView(viewModel)));
			control.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
			preferredViewSize = control.DesiredSize;
			if (preferredViewSize.Width < control.MinWidth)
			{
				preferredViewSize.Width = control.MinWidth;
			}
			if (preferredViewSize.Height < control.MinHeight)
			{
				preferredViewSize.Height = control.MinHeight;
			}
			base.MinWidth = control.MinWidth + 20.0;
			base.MinHeight = control.MinHeight + 38.0;
			base.Width = preferredViewSize.Width + 20.0;
			base.Height = preferredViewSize.Height + 38.0;
			MainGrid.Children.Add(control);
			SystemUtils.SetThreadExecutionMode(ThreadExecutionModes.KeepSystemAwake);
			simulation.Completed += OnSimulationCompleted;
			base.Loaded += OnLoaded;
		}

		private static Simulation GetSimulator(ConfigFile config, Output output, TrajectorySource trajectorySource)
		{
			if (!File.Exists(config.NmeaFile))
			{
				throw new ArgumentException("NMEA file does not exist");
			}
			GnssTime gnssTime = GnssTime.FromUtc(config.Date);
			ILiveOutput liveOutput = output as ILiveOutput;
			if (liveOutput != null && liveOutput.IsTrueTime)
			{
				gnssTime = liveOutput.StartTime - GnssTimeSpan.FromSeconds(1);
				GnssTime startTime = liveOutput.StartTime;
			}
			else
			{
				gnssTime = GnssTime.FromUtc(config.Date);
			}
			Trajectory trajectory = (trajectorySource != TrajectorySource.Joystick) ? new NmeaFileTrajectory(gnssTime, config.NmeaFile, config.GravitationalModel) : new JoystickTrajectory(throttleSlider: Key.Z.IsDownAsync() ? JoystickSlider.ZAxis : (Key.D2.IsDownAsync() ? JoystickSlider.Slider2 : JoystickSlider.Slider1), startTime: gnssTime, nmeaFileName: config.NmeaFile, gravitationalModel: config.GravitationalModel);
			Range<GnssTime, GnssTimeSpan> interval = trajectory.Interval;
			if (interval.Width.Seconds < 1.0)
			{
				string text = trajectory.ErrorMessage;
				if (string.IsNullOrWhiteSpace(text))
				{
					text = "Trajectory is shorter than one second";
				}
				RLLogger.GetLogger().LogMessage(text);
				MessageBox.Show(Application.Current.MainWindow, text, "SatGen error", MessageBoxButton.OK, MessageBoxImage.Hand);
				return null;
			}
			IReadOnlyList<ConstellationBase> readOnlyList = ConstellationBase.Create(config.SignalTypes, output);
			foreach (ConstellationBase item in readOnlyList)
			{
				string almanacPath = GetAlmanacPath(item.ConstellationType, config);
				item.LoadAlmanac(almanacPath, gnssTime);
				AlmanacBase almanac = item.Almanac;
				if (almanac == null || !almanac.BaselineSatellites.Any())
				{
					string text2 = "Invalid " + item.ConstellationType.ToLongName() + " almanac file \"" + Path.GetFileName(almanacPath) + "\"";
					RLLogger.GetLogger().LogMessage(text2);
					MessageBox.Show(Application.Current.MainWindow, text2, "SatGen error", MessageBoxButton.OK, MessageBoxImage.Hand);
					return null;
				}
				AlmanacBase almanac2 = item.Almanac;
				GnssTime simulationTime = interval.Start;
				almanac2.UpdateAlmanacForTime(simulationTime);
			}
			return new DoubleBufferSimulation(new SimulationParams((IReadOnlyList<SignalType>)config.SignalTypes, trajectory, interval, output, readOnlyList, config.Mask, (IDictionary<ConstellationType, double>)config.CN0s, SignalLevelMode.None));
		}

		private static string GetAlmanacPath(ConstellationType constellationType, ConfigFile config)
		{
			switch (constellationType)
			{
			default:
				return config.GpsAlmanacFile;
			case ConstellationType.Glonass:
				return config.GlonassAlmanacFile;
			case ConstellationType.BeiDou:
				return config.BeiDouAlmanacFile;
			case ConstellationType.Galileo:
				return config.GalileoAlmanacFile;
			}
		}

		private static Output GetOutput(ConfigFile config)
		{
			string text = config.OutputFile.ToLower();
			string a = Path.GetExtension(text).ToLowerInvariant();
			if (a == ".bin")
			{
				return new LabSat1Output(config.OutputFile);
			}
			Quantization bitsPerSample = (Quantization)config.BitsPerSample;
			if (a == ".ls3w")
			{
				return new LabSat3wOutput(config.OutputFile, config.SignalTypes, bitsPerSample);
			}
			if (a == ".ls2")
			{
				return new LabSat2Output(config.OutputFile, config.SignalTypes, bitsPerSample);
			}
			if (text == "%labsat2%")
			{
				return new LabSat2LiveOutput(config.SignalTypes, bitsPerSample, isLowLatency: true);
			}
			if (text == "%labsat2rt%")
			{
				DateTime utcNow = DateTime.UtcNow;
				GnssTime value = GnssTime.FromUtc(new DateTime(utcNow.Ticks - utcNow.Ticks % 10000000) + TimeSpan.FromSeconds(10.0));
				return new LabSat2LiveOutput(config.SignalTypes, bitsPerSample, isLowLatency: true, value);
			}
			if (a == ".ls3")
			{
				return new LabSat3Output(config.OutputFile, config.SignalTypes, bitsPerSample);
			}
			throw new ArgumentException("No supported output type for file \"" + config.OutputFile + "\"");
		}

		private void OnSimulationCompleted(object sender, SimulationCompletedEventArgs e)
		{
			simulation.Completed -= OnSimulationCompleted;
			simulation.SimulationParameters.Output.Error -= OnOutputError;
			AppDomain.CurrentDomain.UnhandledException -= OnCurrentDomainUnhandledException;
			TaskScheduler.UnobservedTaskException -= OnUnobservedTaskException;
			if (simulation.SimulationParameters.LiveOutput != null)
			{
				Settings.Default.Save();
			}
			simulation.Dispose();
			SystemUtils.SetThreadExecutionMode();
			if (e.Cancelled)
			{
				Environment.Exit(1);
			}
			Environment.Exit(0);
		}

		private void OnOutputError(object sender, ErrorEventArgs e)
		{
			base.Dispatcher.BeginInvoke((Action)delegate
			{
				MessageBox.Show(this, "Error writing output file. Disk full?", "SatGen error", MessageBoxButton.OK, MessageBoxImage.Hand);
				simulation.Completed -= OnSimulationCompleted;
				simulation.SimulationParameters.Output.Error -= OnOutputError;
				simulation.Dispose();
				AppDomain.CurrentDomain.UnhandledException -= OnCurrentDomainUnhandledException;
				TaskScheduler.UnobservedTaskException -= OnUnobservedTaskException;
				SystemUtils.SetThreadExecutionMode();
				Environment.Exit(-1);
			}, DispatcherPriority.Input);
		}

		protected override void OnClosing(CancelEventArgs e)
		{
			base.OnClosing(e);
			if (simulation?.SimulationParameters.LiveOutput != null)
			{
				Settings.Default.Save();
			}
			if (simulation?.IsAlive ?? false)
			{
				e.Cancel = true;
				base.IsEnabled = false;
				base.Dispatcher.BeginInvoke((Action)delegate
				{
					viewModel.CancelSimulation();
				}, DispatcherPriority.Input);
			}
		}

		private void OnLoaded(object sender, RoutedEventArgs e)
		{
			base.Loaded -= OnLoaded;
			double actualWidth = MainGrid.ActualWidth;
			double actualHeight = MainGrid.ActualHeight;
			base.Width += preferredViewSize.Width - actualWidth + 4.0;
			base.Height += preferredViewSize.Height - actualHeight + 4.0;
			Activate();
			base.Topmost = false;
			base.Dispatcher.BeginInvoke((Action)delegate
			{
				simulation.Start();
			}, DispatcherPriority.SystemIdle);
		}

		private void OnWindowStateChanged(object sender, EventArgs e)
		{
			if (base.WindowState == WindowState.Maximized)
			{
				base.WindowState = WindowState.Normal;
			}
		}

		private void OnSessionEnding(object sender, SessionEndingCancelEventArgs e)
		{
			if (simulation?.IsAlive ?? false)
			{
				simulation.Cancel();
				if (simulation.IsAlive)
				{
					e.Cancel = true;
				}
			}
		}

		private static void OnCurrentDomainUnhandledException(object sender, UnhandledExceptionEventArgs e)
		{
			Environment.Exit(-1);
		}

		private static void OnUnobservedTaskException(object sender, UnobservedTaskExceptionEventArgs e)
		{
			Environment.Exit(-1);
		}

		public void Dispose()
		{
			Dispose(disposing: true);
			GC.SuppressFinalize(this);
		}

		protected virtual void Dispose(bool disposing)
		{
			if (!isDisposed)
			{
				isDisposed = true;
				if (disposing)
				{
					(liveOutput as Output)?.Dispose();
					taskbarItemSemaphore?.Dispose();
					viewModel?.Dispose();
					simulation?.Dispose();
				}
			}
		}
        
	}
}
