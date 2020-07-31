using BPUtil;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ErrorTrackerServer
{
	public class Project
	{
		/// <summary>
		/// Project name.  Must be safe for file names.  Treated in most cases as case-insensitive.
		/// </summary>
		public string Name;
		/// <summary>
		/// URL fragment to use when submitting events to the server.
		/// </summary>
		public string SubmitKey;
		/// <summary>
		/// Max age in days for events.  Events older than this will be deleted automatically. Disabled if less than 1.
		/// </summary>
		public int MaxEventAgeDays = 0;
		/// <summary>
		/// List of Project names to clone incoming events to.  Only the Project named in the Event submission has its CloneTo list processed.
		/// </summary>
		public string[] CloneTo = new string[0];

		public void InitializeSubmitKey()
		{
			SubmitKey = StringUtil.GetRandomAlphaNumericString(40);
		}
	}
}
