using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ErrorTrackerClient
{
	/// <summary>
	/// A Key/Value pair indicating an attribute of an event. Example keys: "IP Address", "User Name", "URL"
	/// </summary>
	public class Tag
	{
		/// <summary>
		/// Key string. Case-insensitive for matching purposes.
		/// </summary>
		public string Key;
		/// <summary>
		/// Value string.
		/// </summary>
		public string Value;
		/// <summary>
		/// Zero-argument constructor for deserialization.
		/// </summary>
		private Tag() { }
		/// <summary>
		/// Constructs a new Tag.
		/// </summary>
		internal Tag(string Key, string Value)
		{
			this.Key = Key;
			this.Value = Value;
		}
		/// <summary>
		/// Returns: Key + ": " + Value
		/// </summary>
		/// <returns></returns>
		public override string ToString()
		{
			return Key + ": " + Value;
		}
	}
	/// <summary>
	/// A Key/Value pair indicating an attribute of an event. Example keys: "IP Address", "User Name", "URL"
	/// </summary>
	public class ReadOnlyTag
	{
		/// <summary>
		/// Key string. Case-insensitive for matching purposes.
		/// </summary>
		public string Key { get; private set; }
		/// <summary>
		/// Value string.
		/// </summary>
		public string Value { get; private set; }
		/// <summary>
		/// Constructs a new Tag.
		/// </summary>
		internal ReadOnlyTag(string Key, string Value)
		{
			this.Key = Key;
			this.Value = Value;
		}
		/// <summary>
		/// Returns: Key + ": " + Value
		/// </summary>
		/// <returns></returns>
		public override string ToString()
		{
			return Key + ": " + Value;
		}
	}
}
