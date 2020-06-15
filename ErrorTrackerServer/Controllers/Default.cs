using BPUtil.MVC;
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
				appPath = "/",
				loginStyle = Settings.data.loginStyle,
				serverVersion = Assembly.GetExecutingAssembly().GetName().Version.ToString()
			};

			ViewData.Set("AppContext", JsonConvert.SerializeObject(appContext));
			ViewData.Set("AppPath", appContext.appPath);

			return View(fi.FullName);
		}
	}
}
