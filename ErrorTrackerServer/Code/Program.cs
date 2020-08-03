using BPUtil;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.ServiceProcess;
using System.Text;
using System.Threading.Tasks;

namespace ErrorTrackerServer
{
	static class Program
	{
		/// <summary>
		/// The main entry point for the application.
		/// </summary>
		static void Main()
		{
			System.Reflection.Assembly assembly = System.Reflection.Assembly.GetEntryAssembly();
			Globals.Initialize(assembly.Location, "Data/");
			Directory.CreateDirectory(Globals.WritableDirectoryBase + "Projects/");
			AppInit.WindowsService<ErrorTrackerSvc>(); // Most of the initialization work happens here, including loading of the Settings.data object. The method blocks, so further initialization should be done in the ErrorTrackerSvc constructor.
		}
	}
}
