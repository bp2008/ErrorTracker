using BPUtil;
using BPUtil.MVC;
using ErrorTrackerServer.Code;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Runtime.InteropServices.ComTypes;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace ErrorTrackerServer.Controllers
{
	public class Wallpaper : ETController
	{

		public ActionResult Index()
		{
			ImageAndHash img = WallpaperDownloader.latestImageFile.GetInstance();
			if (img == null)
				return StatusCode("404 Not Found");
			else if (!string.IsNullOrWhiteSpace(img.hash) && Context.httpProcessor.Request.Headers.Get("If-None-Match") == img.hash)
				return StatusCode("304 Not Modified");
			else
			{
				ActionResult result = new JpegImageResult(img.data);
				result.headers["ETag"] = img.hash;
				return result;
			}
		}
	}
}
