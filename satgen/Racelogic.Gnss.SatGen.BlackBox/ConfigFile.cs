using Racelogic.Geodetics;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;

namespace Racelogic.Gnss.SatGen.BlackBox
{
	public class ConfigFile
	{
		private TimeSpan timeOfDay;

		private int day;

		private int month;

		private int year;

		private SignalType[] signalTypes;

		public string AlmanacFile
		{
			[DebuggerStepThrough]
			get;
			[DebuggerStepThrough]
			protected set;
		}

		public string GpsAlmanacFile
		{
			[DebuggerStepThrough]
			get;
			[DebuggerStepThrough]
			protected set;
		}

		public string GlonassAlmanacFile
		{
			[DebuggerStepThrough]
			get;
			[DebuggerStepThrough]
			protected set;
		}

		public string BeiDouAlmanacFile
		{
			[DebuggerStepThrough]
			get;
			[DebuggerStepThrough]
			protected set;
		}

		public string GalileoAlmanacFile
		{
			[DebuggerStepThrough]
			get;
			[DebuggerStepThrough]
			protected set;
		}

		public DateTime Date => new DateTime(year, month, day) + timeOfDay;

		public GravitationalModel GravitationalModel
		{
			[DebuggerStepThrough]
			get;
			[DebuggerStepThrough]
			protected set;
		}

		public double? Mask
		{
			[DebuggerStepThrough]
			get;
			[DebuggerStepThrough]
			protected set;
		}

		public string NmeaFile
		{
			[DebuggerStepThrough]
			get;
			[DebuggerStepThrough]
			protected set;
		}

		public string OutputFile
		{
			[DebuggerStepThrough]
			get;
			[DebuggerStepThrough]
			protected set;
		}

		public uint BitsPerSample
		{
			[DebuggerStepThrough]
			get;
			[DebuggerStepThrough]
			protected set;
		}

		public bool Rinex
		{
			[DebuggerStepThrough]
			get;
			[DebuggerStepThrough]
			set;
		}

		public Dictionary<ConstellationType, double> CN0s
		{
			[DebuggerStepThrough]
			get;
		} = new Dictionary<ConstellationType, double>();


		public SignalType[] SignalTypes
		{
			[DebuggerStepThrough]
			get
			{
				return signalTypes;
			}
			[DebuggerStepThrough]
			protected set
			{
				signalTypes = value;
			}
		}

		public int SampleRate
		{
			[DebuggerStepThrough]
			get;
			[DebuggerStepThrough]
			protected set;
		}

		public static ConfigFile Read(string filename)
		{
			ConfigFile configFile = new ConfigFile();
			using (StreamReader streamReader = new StreamReader(filename, detectEncodingFromByteOrderMarks: true))
			{
				string text;
				while ((text = streamReader.ReadLine()) != null)
				{
					if (text.IndexOf('=') >= 0)
					{
						string[] array = text.Split('=');
						string text2 = array[1].Trim();
						string text3 = array[1].ToLowerInvariant().Trim();
						string text4 = array[0].ToLowerInvariant().Trim();
						if (text4 != null)
						{
							switch (text4)
							{
							case "source_nmea_file":
								configFile.NmeaFile = text2;
								break;
							case "signal":
								configFile.SignalTypes = (from nst in (from s in text3.Split(',')
								where !string.IsNullOrWhiteSpace(s)
								select s.Trim()).Select((Func<string, SignalType?>)delegate(string s)
								{
									foreach (SignalType value in Enum.GetValues(typeof(SignalType)))
									{
										if (s == value.ToCodeName().ToLowerInvariant())
										{
											return value;
										}
									}
									return null;
								})
								where nst.HasValue
								select nst.Value).SelectMany((SignalType st) => Signal.GetIndividualSignalTypes(st)).Distinct().ToArray();
								break;
							case "gps_start_time_in_sec":
								if (double.TryParse(text2, NumberStyles.Integer, CultureInfo.InvariantCulture, out double result7))
								{
									configFile.timeOfDay = TimeSpan.FromSeconds(result7);
								}
								break;
							case "simulation_day":
								int.TryParse(text2, NumberStyles.Integer, CultureInfo.InvariantCulture, out configFile.day);
								break;
							case "simulation_mon":
								int.TryParse(text2, NumberStyles.Integer, CultureInfo.InvariantCulture, out configFile.month);
								break;
							case "simulation_year":
								int.TryParse(text2, NumberStyles.Integer, CultureInfo.InvariantCulture, out configFile.year);
								break;
							case "almanac_file":
								configFile.AlmanacFile = text2;
								configFile.GpsAlmanacFile = text2;
								break;
							case "almanac_file_gps":
								configFile.GpsAlmanacFile = text2;
								break;
							case "almanac_file_glo":
								configFile.GlonassAlmanacFile = text2;
								break;
							case "almanac_file_bds":
								configFile.BeiDouAlmanacFile = text2;
								break;
							case "almanac_file_gal":
								configFile.GalileoAlmanacFile = text2;
								break;
							case "mask_in_deg":
							case "mask_deg":
								if (double.TryParse(text2, NumberStyles.Integer, CultureInfo.InvariantCulture, out double result8))
								{
									configFile.Mask = result8 / 180.0 * Math.PI;
								}
								break;
							case "if_iq_file":
							case "iq_outputfile":
								configFile.OutputFile = text2;
								break;
							case "bitpersample":
								if (!uint.TryParse(text2, NumberStyles.Integer, CultureInfo.InvariantCulture, out uint result6))
								{
									result6 = 1u;
								}
								configFile.BitsPerSample = result6;
								break;
							case "egm":
								if (text3 != null && !(text3 == "none") && !(text3 == "wgs84"))
								{
									if (text3 == "egm84")
									{
										configFile.GravitationalModel = GravitationalModel.Egm84;
										break;
									}
									if (text3 == "egm96")
									{
										configFile.GravitationalModel = GravitationalModel.Egm96;
										break;
									}
									if (text3 == "egm2008")
									{
										configFile.GravitationalModel = GravitationalModel.Egm2008;
										break;
									}
									if (text3 == "nmea file")
									{
										configFile.GravitationalModel = GravitationalModel.Nmea;
										break;
									}
								}
								configFile.GravitationalModel = GravitationalModel.Wgs84;
								break;
							case "rinex":
								configFile.Rinex = "yes".Equals(text3);
								break;
							case "noisefigure_in_db":
								if (double.TryParse(text2, NumberStyles.Float, CultureInfo.InvariantCulture, out double result5))
								{
									if (!configFile.CN0s.ContainsKey(ConstellationType.Gps))
									{
										configFile.CN0s[ConstellationType.Gps] = result5;
									}
									if (!configFile.CN0s.ContainsKey(ConstellationType.Galileo))
									{
										configFile.CN0s[ConstellationType.Galileo] = result5;
									}
									if (!configFile.CN0s.ContainsKey(ConstellationType.Glonass))
									{
										configFile.CN0s[ConstellationType.Glonass] = result5;
									}
									if (!configFile.CN0s.ContainsKey(ConstellationType.BeiDou))
									{
										configFile.CN0s[ConstellationType.BeiDou] = result5;
									}
								}
								break;
							case "noisefigure_in_db_gps":
								if (double.TryParse(text2, NumberStyles.Float, CultureInfo.InvariantCulture, out double result4))
								{
									configFile.CN0s[ConstellationType.Gps] = result4;
									configFile.CN0s[ConstellationType.Galileo] = result4;
								}
								break;
							case "noisefigure_in_db_glonass":
								if (double.TryParse(text2, NumberStyles.Float, CultureInfo.InvariantCulture, out double result3))
								{
									configFile.CN0s[ConstellationType.Glonass] = result3;
								}
								break;
							case "noisefigure_in_db_beidou":
								if (double.TryParse(text2, NumberStyles.Float, CultureInfo.InvariantCulture, out double result2))
								{
									configFile.CN0s[ConstellationType.BeiDou] = result2;
								}
								break;
							case "samplerate_in_khz":
								if (int.TryParse(text2, NumberStyles.Integer, CultureInfo.InvariantCulture, out int result))
								{
									configFile.SampleRate = 1000 * result;
								}
								break;
							}
						}
					}
				}
			}
			if (configFile.SignalTypes == null || !configFile.SignalTypes.Any())
			{
				configFile.SignalTypes = new SignalType[1]
				{
					SignalType.GpsL1CA
				};
			}
			return configFile;
		}
	}
}
