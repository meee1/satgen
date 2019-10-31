using Racelogic.DataTypes;
using Racelogic.Utilities.Win;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Windows;
using System.Windows.Threading;

namespace Racelogic.Gnss.SatGen.BlackBox
{
	public class SatelliteGroup : BasePropertyChanged, IDisposable
	{
		private readonly Simulation simulation;

		private readonly ConstellationBase constellation;

		private readonly Range<double> attenuationRange = new Range<double>(-30.0, 0.0);

		private bool attenuationsLinked;

		private double linkedAttenuation;

		private bool suppressLinkedAttenuationChange;

		private int satelliteCount;

		private bool isDisposed;

		public ObservableCollection<SatelliteDefinition> Satellites
		{
			[DebuggerStepThrough]
			get;
		} = new ObservableCollection<SatelliteDefinition>();


		public ConstellationType ConstellationType => constellation.ConstellationType;

		public string ConstellationShortName => constellation.ConstellationType.ToShortName();

		public string ConstellationName => constellation.ConstellationType.ToLongName();

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
				if (value != attenuationsLinked)
				{
					attenuationsLinked = value;
					OnPropertyChangedUI("AttenuationsLinked");
				}
			}
		}

		public double LinkedAttenuation
		{
			[DebuggerStepThrough]
			get
			{
				return linkedAttenuation;
			}
			[DebuggerStepThrough]
			set
			{
				linkedAttenuation = value;
				OnPropertyChangedUI("LinkedAttenuation");
			}
		}

		public Range<double> AttenuationRange => attenuationRange;

		public int SatelliteCount
		{
			[DebuggerStepThrough]
			get
			{
				return satelliteCount;
			}
			[DebuggerStepThrough]
			set
			{
				satelliteCount = value;
				OnPropertyChangedUI("SatelliteCount");
				OnPropertyChangedUI("ConstellationAndSatCount");
			}
		}

		public bool IsEnabled
		{
			[DebuggerStepThrough]
			get
			{
				return constellation.IsEnabled;
			}
			[DebuggerStepThrough]
			set
			{
				constellation.IsEnabled = value;
				OnPropertyChangedUI("IsEnabled");
				OnPropertyChangedUI("ConstellationAndSatCount");
			}
		}

		public string ConstellationAndSatCount
		{
			get
			{
				int num = Satellites.Count();
				int num2 = IsEnabled ? (from s in Satellites
				where s.IsEnabled
				select s).Count() : 0;
				if (num2 == num)
				{
					return $"{ConstellationShortName}  ({SatelliteCount})";
				}
				return $"{ConstellationShortName}  ({num2} of {num})";
			}
		}

		public SatelliteGroup(Simulation simulation, ConstellationBase constellation)
		{
			this.simulation = simulation;
			this.constellation = constellation;
			simulation.PropertyChanged += OnSimulation_PropertyChanged;
			UpdateVisibleSatellites();
		}

		private void OnSimulation_PropertyChanged(object sender, PropertyChangedEventArgs e)
		{
			if (e.PropertyName == "VisibleSats")
			{
				UpdateVisibleSatellites();
			}
		}

		private void UpdateVisibleSatellites()
		{
			IEnumerable<SatelliteBase> source = simulation.VisibleSats[constellation.ConstellationType];
			foreach (SatelliteBase item in from s in source
			where !(from ss in Satellites
			select ss.Id).Contains(s.Id)
			select s)
			{
				SatelliteDefinition satelliteDefinition = new SatelliteDefinition(item, simulation.SimulationParameters, this);
				int i;
				for (i = 0; i < Satellites.Count && item.Id > Satellites[i].Id; i++)
				{
				}
				Satellites.Insert(i, satelliteDefinition);
				satelliteDefinition.PropertyChanged += OnSatellite_PropertyChanged;
			}
			SatelliteDefinition[] array = Satellites.ToArray();
			foreach (SatelliteDefinition satDef in array)
			{
				SatelliteBase satelliteBase = source.FirstOrDefault((SatelliteBase s) => s.Id == satDef.Id);
				if (satelliteBase != null)
				{
					if (simulation.SimulationParameters.SatCountLimitMode != 0)
					{
						satDef.IsEnabled = satelliteBase.IsEnabled;
					}
				}
				else
				{
					satDef.PropertyChanged -= OnSatellite_PropertyChanged;
					Satellites.Remove(satDef);
				}
			}
			SatelliteCount = (constellation.IsEnabled ? (from s in Satellites
			where s.IsEnabled
			select s).Count() : 0);
		}

		private void OnSatellite_PropertyChanged(object sender, PropertyChangedEventArgs e)
		{
			if (e.PropertyName == "Attenuation" && AttenuationsLinked && !suppressLinkedAttenuationChange)
			{
				SatelliteDefinition satellite = (SatelliteDefinition)sender;
				if (satellite.IsEnabled)
				{
					suppressLinkedAttenuationChange = true;
					LinkedAttenuation = satellite.Attenuation;
					foreach (SatelliteDefinition item in Satellites.Where(delegate(SatelliteDefinition s)
					{
						if (s != satellite)
						{
							return s.IsEnabled;
						}
						return false;
					}))
					{
						item.Attenuation = LinkedAttenuation;
					}
					Application.Current.Dispatcher.BeginInvoke((Action)delegate
					{
						suppressLinkedAttenuationChange = false;
					}, DispatcherPriority.DataBind);
				}
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
					simulation.PropertyChanged -= OnSimulation_PropertyChanged;
					foreach (SatelliteDefinition satellite in Satellites)
					{
						satellite.PropertyChanged -= OnSatellite_PropertyChanged;
					}
				}
			}
		}
	}
}
