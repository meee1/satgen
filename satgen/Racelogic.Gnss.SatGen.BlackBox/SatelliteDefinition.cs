using Racelogic.Maths;
using Racelogic.Utilities.Win;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;

namespace Racelogic.Gnss.SatGen.BlackBox
{
	public class SatelliteDefinition : BasePropertyChanged
	{
		private readonly int id;

		private readonly int index;

		private readonly ConstellationType constellationType;

		private readonly SimulationParams simulationParameters;

		private readonly SatelliteGroup group;

		private readonly SignalType firstSignalType;

		private double level = 1.0;

		private double attenuation;

		private bool isEnabled = true;

		private double lastAttenuation;

		public int Id => id;

		public int Index => index;

		public string ConstellationShortName => constellationType.ToShortName();

		protected double Level
		{
			[DebuggerStepThrough]
			get
			{
				return level;
			}
			[DebuggerStepThrough]
			set
			{
				if (value != level)
				{
					level = value;
					if (simulationParameters.SignalLevelMode == SignalLevelMode.Manual && firstSignalType != SignalType.None)
					{
						simulationParameters.SignalLevels[firstSignalType][index] = value;
					}
					Attenuation = level.LevelToGain();
					OnPropertyChangedUI("Level");
				}
			}
		}

		public double Attenuation
		{
			[DebuggerStepThrough]
			get
			{
				return attenuation;
			}
			[DebuggerStepThrough]
			set
			{
				if (value != attenuation)
				{
					attenuation = value;
					Level = attenuation.GainToLevel();
					OnPropertyChangedUI("Attenuation");
					OnPropertyChangedUI("AttenuationText");
				}
			}
		}

		public string AttenuationText => Attenuation.ToString("f0", CultureInfo.CurrentCulture) + " dB";

		public bool IsEnabled
		{
			[DebuggerStepThrough]
			get
			{
				return isEnabled;
			}
			[DebuggerStepThrough]
			set
			{
				if (value == isEnabled)
				{
					return;
				}
				isEnabled = value;
				if (isEnabled)
				{
					if (Group.AttenuationsLinked)
					{
						Attenuation = Group.LinkedAttenuation;
					}
					else
					{
						Attenuation = lastAttenuation;
					}
				}
				else
				{
					if (!double.IsNegativeInfinity(Attenuation))
					{
						lastAttenuation = Attenuation;
					}
					Attenuation = double.NegativeInfinity;
				}
				OnPropertyChangedUI("IsEnabled");
			}
		}

		public SatelliteGroup Group => group;

		public SatelliteDefinition(SatelliteBase satellite, SimulationParams simulationParameters, SatelliteGroup group)
		{
			id = satellite.Id;
			index = id - 1;
			constellationType = satellite.ConstellationType;
			this.simulationParameters = simulationParameters;
			this.group = group;
			IEnumerable<SignalType> source = from s in simulationParameters.Signals
			where s.ConstellationType == constellationType
			select s.SignalType;
			firstSignalType = source.FirstOrDefault((SignalType st) => simulationParameters.SignalLevels.ContainsKey(st));
			if (simulationParameters.SignalLevelMode == SignalLevelMode.Manual && firstSignalType != SignalType.None)
			{
				Level = simulationParameters.SignalLevels[firstSignalType][index];
			}
		}
	}
}
