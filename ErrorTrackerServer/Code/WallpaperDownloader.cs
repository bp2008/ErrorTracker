using BPUtil;
using ErrorTrackerServer.Controllers;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;

namespace ErrorTrackerServer.Code
{
	public static class WallpaperDownloader
	{
		public static string wallpaperDirectoryBase => Globals.WritableDirectoryBase + "Wallpapers/";
		public static readonly CachedObject<ImageAndHash> latestImageFile = new CachedObject<ImageAndHash>(ReadLatestImageFile, TimeSpan.FromDays(1), TimeSpan.FromDays(1), ex => Logger.Debug(ex));
		private static WebRequestUtility wru = new WebRequestUtility(null, 30000);
		private static Thread downloaderThread;
		/// <summary>
		/// Set = true to abort the wallpaper downloader thread for the remainder of this process.
		/// </summary>
		public static bool abort = false;

		static WallpaperDownloader()
		{
			downloaderThread = new Thread(ThreadLoop);
			downloaderThread.IsBackground = true;
			downloaderThread.Name = "Wallpaper Downloader";
			downloaderThread.Start();
		}

		/// <summary>
		/// No-op to get the static class loaded.
		/// </summary>
		public static void Initialize()
		{
		}

		private static ImageAndHash ReadLatestImageFile()
		{
			DirectoryInfo di = new DirectoryInfo(wallpaperDirectoryBase);
			if (!di.Exists)
				return null;
			FileInfo[] files = di.GetFiles("bing_*.jpg");
			FileInfo latest = files.OrderByDescending(f => f.CreationTimeUtc).FirstOrDefault();
			if (latest == null)
				return null;
			ImageAndHash img = new ImageAndHash(File.ReadAllBytes(latest.FullName));
			return img;
		}

		private static void ThreadLoop()
		{
			try
			{
				IntervalSleeper sleeper = new IntervalSleeper(500);
				while (!abort)
				{
					try
					{
						if (Settings.data.loginStyle == LoginStyle.wallpaper.ToString())
							DownloadNewBingImage();
					}
					catch (Exception ex)
					{
						Logger.Debug(ex, "Wallpaper downloader thread soft-error.");
					}
					sleeper.SleepUntil((long)TimeSpan.FromHours(2).TotalMilliseconds, () => abort);
				}
			}
			catch (Exception ex)
			{
				Logger.Debug(ex, "Wallpaper downloader thread crashed.");
			}
		}

		private static void DownloadNewBingImage()
		{
			Directory.CreateDirectory(wallpaperDirectoryBase);
			try
			{
				Image img = GetNewImage();
				if (img != null)
				{
					ImageAndHash cached = latestImageFile.GetInstance();
					if (cached != null && ByteUtil.ByteArraysMatch(img.ext_ImageData, cached.data))
					{
						// This is the same image as before.  Do not save a duplicate.
						LogLastImageDownloadString(true, "Image matches what is already cached on disk.");
						return;
					}
					string filePath = wallpaperDirectoryBase + "bing_" + img.startdate + ".jpg";
					if (File.Exists(filePath))
						filePath = wallpaperDirectoryBase + "bing_" + DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss") + ".jpg";
					File.WriteAllBytes(filePath, img.ext_ImageData);
					LogLastImageDownloadString(false, "Image written.");
					latestImageFile.Reload();
				}
			}
			catch (Exception ex)
			{
				Logger.Debug(ex);
				LogLastImageDownloadString(true, ex.ToHierarchicalString());
			}
		}
		private static void LogLastImageDownloadString(bool append, string message)
		{
			string path = wallpaperDirectoryBase + "latest_download_check.txt";
			string text = DateTime.UtcNow.ToString("o") + Environment.NewLine + message + Environment.NewLine + Environment.NewLine;
			if (append)
				File.AppendAllText(path, text);
			else
				File.WriteAllText(path, text);
		}
		private static Image GetNewImage()
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
						img.AddImageData(imgResponse.data);
					else
					{
						imgResponse = wru.GET("https://www.bing.com" + img.url);
						if (imgResponse.StatusCode == 200 && imgResponse.data.Length > 0)
							img.AddImageData(imgResponse.data);
					}

					if (img.ext_ImageData != null)
						return img;
				}
			}
			return null;
		}
	}

	public class ImageAndHash
	{
		public byte[] data;
		public string hash;
		public ImageAndHash(byte[] image)
		{
			this.data = image;
			hash = Hash.GetSHA1Hex(image);
		}
	}

	public class BingWallpapers
	{
		public Image[] Images { get; set; }
	}

	public class Image
	{
		public string startdate;
		//public string fullstartdate;
		public string enddate;
		public string url;
		public string urlbase;
		public string title;
		public string copyright;
		public string copyrightlink;
		public string hsh;
		public byte[] ext_ImageData;

		public void AddImageData(byte[] imageData)
		{
			try
			{
				// Create a JpegBitmapDecoder to read the existing image
				using (MemoryStream inputStream = new MemoryStream(imageData))
				{
					JpegBitmapDecoder decoder = new JpegBitmapDecoder(inputStream, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);

					// Create a new BitmapMetadata object for the JPEG

					BitmapMetadata metadata = new BitmapMetadata("jpg");
					metadata.Author = new System.Collections.ObjectModel.ReadOnlyCollection<string>(new string[] { copyrightlink });
					metadata.Comment = "https://www.bing.com" + url;
					metadata.Title = title;
					metadata.Copyright = copyright;
					metadata.Subject = "Bing image from " + startdate;

					// Create a JpegBitmapEncoder to write the modified image
					JpegBitmapEncoder encoder = new JpegBitmapEncoder();
					encoder.Frames.Add(BitmapFrame.Create(decoder.Frames[0], decoder.Frames[0].Thumbnail, metadata, decoder.ColorContexts));

					// Save the image with updated metadata to a new file
					using (MemoryStream outputStream = new MemoryStream())
					{
						encoder.Save(outputStream);
						ext_ImageData = outputStream.ToArray();
					}
				}
			}
			catch (Exception ex)
			{
				Logger.Debug(ex, "Failed to add metadata to image.");
				ext_ImageData = imageData;
			}
		}
	}
}
