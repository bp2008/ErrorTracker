using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ErrorTrackerClient
{
	/// <summary>
	/// Provides utilities for working with strings.
	/// </summary>
	public static class StringUtil
	{
		/// <summary>
		/// Returns true if the string meets minimum reasonable criteria for a printable display name, meaning it consists of at least one alphanumeric character among any number of spaces or other ASCII-printable characters.
		/// </summary>
		/// <param name="str">String to test.</param>
		/// <returns></returns>
		public static bool IsPrintableName(string str)
		{
			if (str == null)
				return false;
			bool containsAlphaNumeric = false;
			foreach (char c in str)
			{
				if ((c >= 'a' && c <= 'z')
					|| (c >= 'A' && c <= 'Z')
					|| (c >= '0' && c <= '9'))
				{
					containsAlphaNumeric = true;
					// Character is OK
				}
				else if (c >= 32 && c <= 126)
				{
					// Character is OK
				}
				else
					return false;
			}
			return containsAlphaNumeric;
		}
	}
}
