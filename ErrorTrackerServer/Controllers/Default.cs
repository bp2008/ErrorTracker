using BPUtil.MVC;
using ErrorTrackerServer.Code;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace ErrorTrackerServer.Controllers
{
	public class Default : ETController
	{
		private static string[] scriptFiles = new string[] { "runtime.js", "vendors.js", "main.js" };
		public ActionResult Index()
		{
			DirectoryInfo WWWDirectory = new DirectoryInfo(Settings.data.GetWWWDirectoryBase());
			string wwwDirectoryBase = WWWDirectory.FullName.Replace('\\', '/').TrimEnd('/') + '/';
			FileInfo fi = new FileInfo(wwwDirectoryBase + "Default.html");
			if (!fi.Exists)
				return StatusCode("404 Not Found");

			dynamic appContext = new
			{
				systemName = "Error Tracker",
				appPath = Settings.data.GetAppPath(),
				loginStyle = Settings.data.loginStyle,
				serverVersion = Assembly.GetExecutingAssembly().GetName().Version.ToString()
			};

			ViewData.Set("AppContext", JsonConvert.SerializeObject(appContext));
			ViewData.Set("AppPath", appContext.appPath);

			WebpackChunkResolver webpackChunkResolver = new WebpackChunkResolver();
			StringBuilder scriptCallouts = new StringBuilder();
			foreach (string scriptName in scriptFiles)
			{
				scriptCallouts.Append("<script src=\"");
				scriptCallouts.Append(appContext.appPath);
				scriptCallouts.Append("dist/");
				scriptCallouts.Append(webpackChunkResolver.Resolve(scriptName));
				scriptCallouts.AppendLine("\"></script>");
			}
			ViewData.Set("ScriptCallouts", scriptCallouts.ToString());

			return View(fi.FullName);
		}
	}
}
