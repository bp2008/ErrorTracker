using BPUtil;
using SQLite;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace ErrorTrackerServer.Database.Model
{
	/// <summary>
	/// A folder which events must be sorted into.
	/// </summary>
	public class Folder
	{
		/// <summary>
		/// Auto-incremented unique identifier for the Folder.
		/// </summary>
		[PrimaryKey, AutoIncrement]
		public int FolderId { get; set; }
		/// <summary>
		/// ID of the parent folder. The root folder is the parent of itself.
		/// </summary>
		public int ParentFolderId { get; set; }
		/// <summary>
		/// Name of the folder.
		/// </summary>
		[NotNull]
		public string Name { get; set; }

		public Folder() { }
		public Folder(string Name, int ParentFolderId)
		{
			this.Name = Name;
			this.ParentFolderId = ParentFolderId;
		}

		private static Regex rxBadFolderName = new Regex("[" + Regex.Escape(new string(Path.GetInvalidFileNameChars())) + "]", RegexOptions.Compiled);
		/// <summary>
		/// Returns true if the specified string is a valid folder name.
		/// </summary>
		/// <param name="name">Name to validate.</param>
		/// <returns></returns>
		public static bool ValidateName(string name)
		{
			if (string.IsNullOrWhiteSpace(name) || rxBadFolderName.IsMatch(name))
				return false;
			return StringUtil.IsPrintableName(name);
		}
	}
}
