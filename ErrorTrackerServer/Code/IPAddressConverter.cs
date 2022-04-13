using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace ErrorTrackerServer.Code
{
	/// <summary>
	/// IPAddressConverter is a Newtonsoft.Json.JsonConverter which converts IPAddress to and from a string.
	/// Json.net can't serialize IP Addresses without an explicit converter like this one.
	/// </summary>
	public class IPAddressConverter : JsonConverter
	{
		public override bool CanConvert(Type objectType)
		{
			return objectType == typeof(IPAddress) || objectType == typeof(IPAddress);
		}

		public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
		{
			writer.WriteValue(value.ToString());
		}

		public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
		{
			return IPAddress.Parse((string)reader.Value);
		}
	}
}
