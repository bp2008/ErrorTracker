using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ErrorTrackerServer.SQLiteMigration
{
	public sealed class HexStringJsonConverter : JsonConverter
	{
		private int maxBytes;
		public override bool CanConvert(Type objectType)
		{
			return typeof(uint).Equals(objectType) || typeof(int).Equals(objectType);
		}

		public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
		{
			string str = ((uint)value).ToString("X").PadLeft(8, '0');
			if (maxBytes < 4)
				str = str.Substring((4 - maxBytes) * 2);
			writer.WriteValue(str);
		}

		public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
		{
			var str = reader.Value.ToString();
			if (string.IsNullOrWhiteSpace(str))
				throw new JsonSerializationException();

			if (typeof(uint).Equals(objectType))
				return Convert.ToUInt32(str, 16);
			else
				return Convert.ToInt32(str, 16);
		}
		/// <summary>
		/// Creates a JsonConverter which serializes a uint/int field as a hexidecimal string.
		/// </summary>
		/// <param name="maxBytes">Number of bytes to include in serializer output (default: 4). If less than 4, the MOST SIGNIFICANT bytes are dropped first.</param>
		public HexStringJsonConverter(int maxBytes = 4)
		{
			if (maxBytes < 1 || maxBytes > 4)
				throw new ArgumentOutOfRangeException("maxBytes", maxBytes, "Value must be between 1 and 4.");
			this.maxBytes = maxBytes;
		}
	}
}
