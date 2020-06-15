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

		public void InitializeSubmitKey()
		{
			SubmitKey = StringUtil.GetRandomAlphaNumericString(40);
		}
	}
}
