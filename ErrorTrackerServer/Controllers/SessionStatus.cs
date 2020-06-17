using BPUtil;
using BPUtil.MVC;
using ErrorTrackerServer.Database.Model;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ErrorTrackerServer.Controllers
{
	public class SessionStatus : UserController
	{
		public ActionResult IsSessionActive()
		{
			return Json(new ApiResponseBase(true));
		}
	}
}
