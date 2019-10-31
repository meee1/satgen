using Racelogic.Utilities;
using System;
using System.CodeDom.Compiler;
using System.Diagnostics;
using System.Reflection;
using System.Windows;
using Harmony;

namespace Racelogic.Gnss.SatGen.BlackBox
{
	public class App : Application
	{
		protected override void OnStartup(StartupEventArgs e)
		{
			base.OnStartup(e);
			RLLogger.GetLogger().Initialize("SatGen3", useLocalAppDataFolder: true);
			RLLogger.GetLogger().LogUnhandledExceptions();
		}


		public void InitializeComponent()
		{
			base.StartupUri = new Uri("MainWindow.xaml", UriKind.Relative);
		}

		[STAThread]
		public static void Main()
		{
            var harmony = HarmonyInstance.Create("com.company.project.product");
            var original = typeof(Racelogic.Gnss.SatGen.Simulation).GetMethod("CheckFeature", BindingFlags.NonPublic | BindingFlags.Static);
            Console.WriteLine(original);
            var prefix = typeof(App).GetMethod("CheckFeature_pre", BindingFlags.Static | BindingFlags.NonPublic);
            Console.WriteLine(prefix);
            var postfix = typeof(App).GetMethod("CheckFeature_post", BindingFlags.Static | BindingFlags.NonPublic);
            Console.WriteLine(postfix);

            harmony.Patch(original, new HarmonyMethod(prefix));

            App app = new App();
			app.InitializeComponent();
			app.Run();
		}

        private static bool CheckFeature_pre(ref bool __result)
        {
            __result = true;
            return false;
        }

        private static void CheckFeature_post()
        {
           
        }
    }
}
