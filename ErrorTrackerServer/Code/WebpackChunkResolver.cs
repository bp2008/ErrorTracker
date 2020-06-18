using BPUtil;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ErrorTrackerServer.Code
{
	/// <summary>
	/// Provides clean access to the dist/manifest.json file, which maps known file names to actual file names containing hash values.
	/// </summary>
	public class WebpackChunkResolver
	{
		protected dynamic manifest;
		public WebpackChunkResolver()
		{
			string filePath = Globals.ApplicationDirectoryBase + "www/dist/manifest.json";
			try
			{
				if (File.Exists(filePath))
				{
					string manifestStr = File.ReadAllText(filePath);
					manifest = Newtonsoft.Json.JsonConvert.DeserializeObject(manifestStr);
				}
				else
				{
#if !DEBUG
					// This can happen if you load the web app in a browser before webpack has filled the dist folder.  File name hashes are disabled anyway in debug builds.
					Logger.Debug("WebpackChunkResolver could not find manifest file \"" + filePath + "\" and is therefore unable to map script file paths properly.");
#endif
				}
			}
			catch (Exception ex)
			{
				Logger.Debug("WebpackChunkResolver could not read manifest file from \"" + filePath + "\". " + ex.ToString());
			}
		}
		/// <summary>
		/// Given a path relative to dist ("commons.js") returns the path to the actual file which may contain a hash value ("commons.0e256e60ac5cd3ab22f9.js").
		/// Returns the input [relativePath] unmodified upon failure.
		/// </summary>
		/// <param name="relativePath">A path relative to the /dist/ directory. e.g. "commons.js"</param>
		/// <returns>Returns the path of the file. e.g. "commons.0e256e60ac5cd3ab22f9.js". Returns the input [relativePath] unmodified upon failure.</returns>
		public string Resolve(string relativePath)
		{
			try
			{
				if (manifest != null)
				{
					string resolved = (string)manifest[relativePath];
					if (resolved != null)
					{
						resolved = resolved.TrimStart('/');
						if (resolved.StartsWith("dist/", StringComparison.OrdinalIgnoreCase))
							resolved = resolved.Substring("dist/".Length);
						return resolved;
					}
				}
			}
			catch (Exception ex)
			{
				Logger.Debug("WebpackChunkResolver could not resolve item \"" + relativePath + "\". " + ex.ToString());
			}
			return relativePath;
		}
	}
}
