using BPUtil;
using BPUtil.MVC;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace ErrorTrackerServer.Controllers
{
	public class Wallpaper : ETController
	{
		private static WebRequestUtility wru = new WebRequestUtility(null);
		public Image wallpaperCache = null;
		public ActionResult Index()
		{
			Image img = wallpaperCache;
			if (img == null)
				UpdateWallpaperCache();
			img = wallpaperCache;
			if (img == null)
				return StatusCode("404 Not Found");
			else if (!string.IsNullOrWhiteSpace(img.hsh) && Context.httpProcessor.GetHeaderValue("If-None-Match") == img.hsh)
				return StatusCode("304 Not Modified");
			else
			{
				ActionResult result = new JpegImageResult(img.ext_ImageData);

				if (!string.IsNullOrWhiteSpace(img.hsh))
					result.AddOrUpdateHeader("ETag", img.hsh);

				result.AddOrUpdateHeader("Copyright", img.copyright);
				result.AddOrUpdateHeader("Link", img.copyrightlink);
				result.AddOrUpdateHeader("Title", img.title);

				return result;
			}
		}
		private void UpdateWallpaperCache()
		{
			BpWebResponse jsonResponse = wru.GET("https://www.bing.com/HPImageArchive.aspx?format=js&idx=0&n=1&mkt=en-US");
			if (jsonResponse.StatusCode == 200 && jsonResponse.str.Length > 0)
			{
				BingWallpapers wp = JsonConvert.DeserializeObject<BingWallpapers>(jsonResponse.str);
				if (wp.Images != null && wp.Images.Length > 0)
				{
					Image img = wp.Images[0];
					BpWebResponse imgResponse = wru.GET("https://www.bing.com" + img.urlbase + "_1920x1080.jpg");
					if (imgResponse.StatusCode == 200 && imgResponse.data.Length > 0)
						img.ext_ImageData = imgResponse.data;
					else
					{
						imgResponse = wru.GET("https://www.bing.com" + img.url);
						if (imgResponse.StatusCode == 200 && imgResponse.data.Length > 0)
							img.ext_ImageData = imgResponse.data;
					}

					if (img.ext_ImageData != null)
						wallpaperCache = img;
				}
			}
		}
	}

	public class BingWallpapers
	{
		public Image[] Images { get; set; }
	}

	public class Image
	{
		//public string startdate;
		//public string fullstartdate;
		public string enddate;
		public string url;
		public string urlbase;
		public string title;
		public string copyright;
		public string copyrightlink;
		public string hsh;
		public byte[] ext_ImageData;
	}
}
