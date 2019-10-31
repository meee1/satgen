using Racelogic.Geodetics;
using Racelogic.Gnss.SatGen.BlackBox.Properties;
using Racelogic.Maths;
using Racelogic.Utilities;
using Racelogic.Utilities.Win;
using Racelogic.WPF.Utilities;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Shell;
using System.Windows.Threading;

namespace Racelogic.Gnss.SatGen.BlackBox
{
	public class SimulationViewModel : Racelogic.Utilities.Win.BasePropertyChanged, IDisposable
	{
		private readonly Simulation simulation;

		private readonly ILiveOutput liveOutput;

		private bool closeRequested;

		private readonly ProgressEstimator progressEstimator = new ProgressEstimator();

		private string progressMessage;

		private int lastSimulationProgressWholeSeconds;

		private ConnectionStatus connectionStatus;

		private TaskbarItemProgressState progressState = TaskbarItemProgressState.Normal;

		private readonly SemaphoreSlim bufferUnderrunSemaphore = new SemaphoreSlim(1);

		private const int bufferUnderrunTimeout = 2000;

		private bool firstFewSeconds = true;

		private readonly TimeSpan firstFewSecondsPeriod = TimeSpan.FromSeconds(4.0);

		private int bufferUnderrunCount;

		private readonly SatelliteGroup[] visibleSatellites;

		private bool attenuationsLinked;

		private string title;

		private const string satGenName = "SatGen";

		private const string satGenRealTimeName = "SatGen Real Time";

		private readonly ICommand resetSatCountLimitCommand;

		private bool suppressBufferUnderrunCountReset;

		private SatCountLimitMode lastSatCountLimitMode = SatCountLimitMode.Automatic;

		private static readonly Dispatcher dispatcher = Application.Current.Dispatcher;

		private bool isDisposed;

		public Simulation Simulation => simulation;

		public ConnectionStatus ConnectionStatus
		{
			[DebuggerStepThrough]
			get
			{
				return connectionStatus;
			}
			[DebuggerStepThrough]
			set
			{
				connectionStatus = value;
				ProgressState = ConnectionStatusToProgressState(value);
				OnPropertyChangedUI("ConnectionStatus");
			}
		}

		public TaskbarItemProgressState ProgressState
		{
			[DebuggerStepThrough]
			get
			{
				return progressState;
			}
			[DebuggerStepThrough]
			set
			{
				progressState = value;
				OnPropertyChangedUI("ProgressState");
			}
		}

		public double Progress
		{
			[DebuggerStepThrough]
			get
			{
				return progressEstimator.Progress;
			}
			[DebuggerStepThrough]
			set
			{
				progressEstimator.Progress = value;
				OnPropertyChangedUI("Progress");
			}
		}

		public string ProgressMessage
		{
			[DebuggerStepThrough]
			get
			{
				return progressMessage;
			}
			[DebuggerStepThrough]
			set
			{
				progressMessage = value;
				OnPropertyChangedUI("ProgressMessage");
			}
		}

		public IEnumerable<SatelliteGroup> VisibleSatellites => visibleSatellites;

		public bool AttenuationsLinked
		{
			[DebuggerStepThrough]
			get
			{
				return attenuationsLinked;
			}
			[DebuggerStepThrough]
			set
			{
				if (value == attenuationsLinked)
				{
					return;
				}
				attenuationsLinked = value;
				if (visibleSatellites != null)
				{
					SatelliteGroup[] array = visibleSatellites;
					for (int i = 0; i < array.Length; i++)
					{
						array[i].AttenuationsLinked = value;
					}
				}
				if (simulation.SimulationParameters.Output is ILiveOutput)
				{
					Settings.Default.LiveAttenuationsLinked = value;
				}
				OnPropertyChangedUI("AttenuationsLinked");
			}
		}

		public string Title
		{
			[DebuggerStepThrough]
			get
			{
				return title;
			}
			[DebuggerStepThrough]
			private set
			{
				title = value;
				OnPropertyChangedUI("Title");
			}
		}

		public int BufferUnderrunCount
		{
			[DebuggerStepThrough]
			get
			{
				return bufferUnderrunCount;
			}
			[DebuggerStepThrough]
			set
			{
				bufferUnderrunCount = value;
				OnPropertyChangedUI("BufferUnderrunCount");
			}
		}

		public bool IsSatCountLimitEnabled
		{
			[DebuggerStepThrough]
			get
			{
				return SatCountLimitMode != SatCountLimitMode.None;
			}
			[DebuggerStepThrough]
			set
			{
				if ((value && SatCountLimitMode == SatCountLimitMode.None) || (!value && SatCountLimitMode != 0))
				{
					SatCountLimitMode = (value ? lastSatCountLimitMode : SatCountLimitMode.None);
					OnPropertyChangedUI("IsSatCountLimitEnabled");
				}
			}
		}

		public SatCountLimitMode SatCountLimitMode
		{
			[DebuggerStepThrough]
			get
			{
				return simulation.SimulationParameters.SatCountLimitMode;
			}
			[DebuggerStepThrough]
			set
			{
				if (value != 0)
				{
					lastSatCountLimitMode = value;
				}
				if (value != simulation.SimulationParameters.SatCountLimitMode)
				{
					simulation.SimulationParameters.SatCountLimitMode = value;
					if (value != 0 && VisibleSatellites != null)
					{
						foreach (SatelliteDefinition item in VisibleSatellites.SelectMany((SatelliteGroup v) => v.Satellites))
						{
							item.IsEnabled = true;
						}
					}
					if (simulation.SimulationParameters.Output is ILiveOutput)
					{
						Settings.Default.LiveSatCountLimitMode = (int)value;
					}
					OnPropertyChangedUI("SatCountLimitMode");
				}
			}
		}

		public int AutomaticSatCountLimit
		{
			[DebuggerStepThrough]
			get
			{
				return simulation.SimulationParameters.AutomaticSatCountLimit;
			}
			[DebuggerStepThrough]
			set
			{
				if (!suppressBufferUnderrunCountReset)
				{
					BufferUnderrunCount = 0;
				}
				simulation.SimulationParameters.AutomaticSatCountLimit = value;
			}
		}

		public ICommand ResetSatCountLimit => resetSatCountLimitCommand;

		public int GpsSatCountLimit
		{
			[DebuggerStepThrough]
			get
			{
				return simulation.SimulationParameters.SatCountLimits[ConstellationType.Gps];
			}
			[DebuggerStepThrough]
			set
			{
				if (!simulation.SimulationParameters.SatCountLimits.TryGetValue(ConstellationType.Gps, out int value2))
				{
					value2 = int.MinValue;
				}
				if (value != value2)
				{
					simulation.SimulationParameters.SatCountLimits[ConstellationType.Gps] = value;
					if (simulation.SimulationParameters.Output is ILiveOutput)
					{
						Settings.Default.LiveGpsSatCountLimit = value;
					}
					OnPropertyChangedUI("GpsSatCountLimit");
				}
			}
		}

		public int GlonassSatCountLimit
		{
			[DebuggerStepThrough]
			get
			{
				return simulation.SimulationParameters.SatCountLimits[ConstellationType.Glonass];
			}
			[DebuggerStepThrough]
			set
			{
				if (!simulation.SimulationParameters.SatCountLimits.TryGetValue(ConstellationType.Glonass, out int value2))
				{
					value2 = int.MinValue;
				}
				if (value != value2)
				{
					simulation.SimulationParameters.SatCountLimits[ConstellationType.Glonass] = value;
					if (simulation.SimulationParameters.Output is ILiveOutput)
					{
						Settings.Default.LiveGlonassSatCountLimit = value;
					}
					OnPropertyChangedUI("GlonassSatCountLimit");
				}
			}
		}

		public int BeiDouSatCountLimit
		{
			[DebuggerStepThrough]
			get
			{
				return simulation.SimulationParameters.SatCountLimits[ConstellationType.BeiDou];
			}
			[DebuggerStepThrough]
			set
			{
				if (!simulation.SimulationParameters.SatCountLimits.TryGetValue(ConstellationType.BeiDou, out int value2))
				{
					value2 = int.MinValue;
				}
				if (value != value2)
				{
					simulation.SimulationParameters.SatCountLimits[ConstellationType.BeiDou] = value;
					if (simulation.SimulationParameters.Output is ILiveOutput)
					{
						Settings.Default.LiveBeiDouSatCountLimit = value;
					}
					OnPropertyChangedUI("BeiDouSatCountLimit");
				}
			}
		}

		public int GalileoSatCountLimit
		{
			[DebuggerStepThrough]
			get
			{
				return simulation.SimulationParameters.SatCountLimits[ConstellationType.Galileo];
			}
			[DebuggerStepThrough]
			set
			{
				if (!simulation.SimulationParameters.SatCountLimits.TryGetValue(ConstellationType.Galileo, out int value2))
				{
					value2 = int.MinValue;
				}
				if (value != value2)
				{
					simulation.SimulationParameters.SatCountLimits[ConstellationType.Galileo] = value;
					if (simulation.SimulationParameters.Output is ILiveOutput)
					{
						Settings.Default.LiveGalileoSatCountLimit = value;
					}
					OnPropertyChangedUI("GalileoSatCountLimit");
				}
			}
		}

		public bool IsGpsPresent => simulation.VisibleSats.ContainsKey(ConstellationType.Gps);

		public bool IsGlonassPresent => simulation.VisibleSats.ContainsKey(ConstellationType.Glonass);

		public bool IsBeiDouPresent => simulation.VisibleSats.ContainsKey(ConstellationType.BeiDou);

		public bool IsGalileoPresent => simulation.VisibleSats.ContainsKey(ConstellationType.Galileo);

		public SimulationViewModel(Simulation simulation)
		{
			this.simulation = simulation;
			simulation.SimulationParameters.Output.PropertyChanged += OnOutputPropertyChanged;
			liveOutput = simulation.SimulationParameters.LiveOutput;
			if (liveOutput != null)
			{
				liveOutput.BufferUnderrun += OnBufferUnderrun;
				ConnectionStatus = ConnectionStatus.Connected;
				SatCountLimitMode = (SatCountLimitMode)Settings.Default.LiveSatCountLimitMode;
				AutomaticSatCountLimit = Settings.Default.LiveAutomaticSatCountLimit;
				GpsSatCountLimit = Settings.Default.LiveGpsSatCountLimit;
				GlonassSatCountLimit = Settings.Default.LiveGlonassSatCountLimit;
				BeiDouSatCountLimit = Settings.Default.LiveBeiDouSatCountLimit;
				GalileoSatCountLimit = Settings.Default.LiveGalileoSatCountLimit;
				attenuationsLinked = Settings.Default.LiveAttenuationsLinked;
			}
			else
			{
				SatCountLimitMode = SatCountLimitMode.Manual;
				AutomaticSatCountLimit = int.MaxValue;
				GpsSatCountLimit = int.MaxValue;
				GlonassSatCountLimit = int.MaxValue;
				BeiDouSatCountLimit = int.MaxValue;
				GalileoSatCountLimit = int.MaxValue;
			}
			simulation.ProgressChanged += OnProgressChanged;
			simulation.Completed += OnSimulationCompleted;
			simulation.SimulationParameters.PropertyChanged += OnSimulationParametersPropertyChanged;
			List<SatelliteGroup> list = new List<SatelliteGroup>();
			ConstellationBase constellationBase = simulation.SimulationParameters.Constellations.FirstOrDefault((ConstellationBase c) => c.ConstellationType == ConstellationType.Gps);
			if (constellationBase != null)
			{
				list.Add(new SatelliteGroup(simulation, constellationBase));
			}
			ConstellationBase constellationBase2 = simulation.SimulationParameters.Constellations.FirstOrDefault((ConstellationBase c) => c.ConstellationType == ConstellationType.Galileo);
			if (constellationBase2 != null)
			{
				list.Add(new SatelliteGroup(simulation, constellationBase2));
			}
			ConstellationBase constellationBase3 = simulation.SimulationParameters.Constellations.FirstOrDefault((ConstellationBase c) => c.ConstellationType == ConstellationType.Glonass);
			if (constellationBase3 != null)
			{
				list.Add(new SatelliteGroup(simulation, constellationBase3));
			}
			ConstellationBase constellationBase4 = simulation.SimulationParameters.Constellations.FirstOrDefault((ConstellationBase c) => c.ConstellationType == ConstellationType.BeiDou);
			if (constellationBase4 != null)
			{
				list.Add(new SatelliteGroup(simulation, constellationBase4));
			}
			visibleSatellites = list.ToArray();
			SatelliteGroup[] array = visibleSatellites;
			for (int i = 0; i < array.Length; i++)
			{
				array[i].PropertyChanged += OnSatelliteGroupPropertyChanged;
			}
			attenuationsLinked = !attenuationsLinked;
			AttenuationsLinked = !attenuationsLinked;
			resetSatCountLimitCommand = new RelayCommand(delegate
			{
				AutomaticSatCountLimit = 99;
				BufferUnderrunCount = 0;
			}, (object p) => SatCountLimitMode == SatCountLimitMode.Automatic);
		}

		public void CancelSimulation()
		{
			closeRequested = true;
			simulation.Cancel();
		}

		private void OnSatelliteGroupPropertyChanged(object sender, PropertyChangedEventArgs e)
		{
			if (e.PropertyName == "SatelliteCount")
			{
				UpdateTitle(lastSimulationProgressWholeSeconds);
			}
		}

		private void OnProgressChanged(object sender, SimulationProgressChangedEventArgs e)
		{
			if (closeRequested)
			{
				Environment.Exit(0);
			}
			double num = (e.Time - simulation.SimulationParameters.Interval.Start).Seconds;
			Progress = num / simulation.SimulationParameters.Interval.Width.Seconds;
			if (liveOutput != null)
			{
				num -= (double)Simulation.ConcurrentSlicesCount * simulation.SimulationParameters.SliceLength + 0.2;
			}
			int num2 = (int)num.SafeFloor();
			if (num2 > lastSimulationProgressWholeSeconds)
			{
				lastSimulationProgressWholeSeconds = num2;
				if (progressEstimator.TimeLeft == TimeSpan.Zero)
				{
					ProgressMessage = $"{progressEstimator.Progress:P1} processed";
				}
				else if (progressEstimator.TimeLeft.Days == 0)
				{
					ProgressMessage = $"{progressEstimator.Progress:P1} processed, {progressEstimator.TimeLeft:hh\\:mm\\:ss} until completion";
				}
				else
				{
					ProgressMessage = $"{progressEstimator.Progress:P1} processed, {progressEstimator.TimeLeft.Days:d} days {progressEstimator.TimeLeft:hh\\:mm\\:ss} until completion";
				}
				UpdateTitle(num2);
				if (progressEstimator.ElapsedTime >= firstFewSecondsPeriod)
				{
					firstFewSeconds = false;
				}
			}
		}

		private void UpdateTitle(double simulationProgressSeconds)
		{
			int num = (from g in visibleSatellites
			select g.SatelliteCount).Sum();
			GnssTime gnssTime = simulation.SimulationParameters.Interval.Start + GnssTimeSpan.FromSeconds(simulationProgressSeconds);
			string text = (Simulation.SimulationParameters.Output is ILiveOutput) ? "SatGen Real Time" : "SatGen";
			TimeSpan timeSpan = TimeSpan.FromSeconds(simulationProgressSeconds);
			Title = $"{text} - {num} satellites - {gnssTime} UTC  (+{timeSpan})";
		}

		private void OnSimulationParametersPropertyChanged(object sender, PropertyChangedEventArgs e)
		{
			if (e.PropertyName == "AutomaticSatCountLimit")
			{
				if (simulation.SimulationParameters.Output is ILiveOutput)
				{
					Settings.Default.LiveAutomaticSatCountLimit = simulation.SimulationParameters.AutomaticSatCountLimit;
				}
				dispatcher.Invoke(delegate
				{
					suppressBufferUnderrunCountReset = true;
					OnPropertyChangedUI("AutomaticSatCountLimit");
					suppressBufferUnderrunCountReset = false;
				}, DispatcherPriority.Normal);
			}
		}

		private async void OnBufferUnderrun(object sender, EventArgs e)
		{
			if (!firstFewSeconds && bufferUnderrunSemaphore.Wait(0))
			{
				ConnectionStatus = ConnectionStatus.BufferUnderrun;
				BufferUnderrunCount++;
				await Task.Delay(2000);
				ILiveOutput liveOutput = simulation?.SimulationParameters.Output as ILiveOutput;
				if (liveOutput != null)
				{
					ConnectionStatus = ((!liveOutput.IsAlive) ? ConnectionStatus.Connected : ConnectionStatus.Transmitting);
				}
				else
				{
					ConnectionStatus = ConnectionStatus.None;
				}
				bufferUnderrunSemaphore?.Release();
			}
		}

		private void OnConnectionLost(object sender, EventArgs e)
		{
			RLLogger.GetLogger().LogMessage("Setting ConnectionStatus to ConnectionLost");
			ConnectionStatus = ConnectionStatus.ConnectionLost;
		}

		private void OnOutputPropertyChanged(object sender, PropertyChangedEventArgs e)
		{
			if (e.PropertyName == "IsAlive")
			{
				if (((ILiveOutput)sender).IsAlive)
				{
					ConnectionStatus = ConnectionStatus.Transmitting;
				}
				else
				{
					ConnectionStatus = ConnectionStatus.Connected;
				}
			}
		}

		private static TaskbarItemProgressState ConnectionStatusToProgressState(ConnectionStatus status)
		{
			switch (status)
			{
			case ConnectionStatus.Connected:
				return TaskbarItemProgressState.Paused;
			case ConnectionStatus.BufferUnderrun:
				return TaskbarItemProgressState.Error;
			case ConnectionStatus.Transmitting:
				return TaskbarItemProgressState.Normal;
			default:
				return TaskbarItemProgressState.None;
			}
		}

		private void OnSimulationCompleted(object sender, SimulationCompletedEventArgs e)
		{
			UnsubscribeSimulationEvents();
		}

		private void UnsubscribeSimulationEvents()
		{
			simulation.ProgressChanged -= OnProgressChanged;
			simulation.Completed -= OnSimulationCompleted;
			simulation.SimulationParameters.PropertyChanged -= OnSimulationParametersPropertyChanged;
			simulation.SimulationParameters.Output.PropertyChanged -= OnOutputPropertyChanged;
			if (liveOutput != null)
			{
				liveOutput.BufferUnderrun -= OnBufferUnderrun;
			}
			SatelliteGroup[] array = visibleSatellites;
			for (int i = 0; i < array.Length; i++)
			{
				array[i].PropertyChanged -= OnSatelliteGroupPropertyChanged;
			}
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
					bufferUnderrunSemaphore?.Dispose();
					UnsubscribeSimulationEvents();
				}
			}
		}
	}
}
