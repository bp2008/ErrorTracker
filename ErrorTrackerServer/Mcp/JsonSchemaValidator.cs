using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ErrorTrackerServer.Mcp
{
	/// <summary>
	/// <para>Validates JSON values against the subset of JSON Schema (draft 2020-12) used by this server's tool input schemas:
	/// type, properties, required, additionalProperties (boolean), enum, const, minimum, maximum, minLength, maxLength, pattern, minItems, maxItems, items, oneOf and anyOf.</para>
	/// <para>Annotation keywords (description, title, default, examples) are ignored.</para>
	/// <para>Each problem is reported with the path of the offending argument, e.g. "`conditions[1].operator`: must be one of: ...".</para>
	/// </summary>
	public static class JsonSchemaValidator
	{
		/// <summary>
		/// Returns a list of problems (empty if the value is valid).
		/// </summary>
		/// <param name="schema">Schema.</param>
		/// <param name="value">Value to validate.</param>
		/// <param name="path">Path of the value relative to the tool arguments, used in error messages.  Empty for the arguments object itself.</param>
		public static List<string> Validate(JObject schema, JToken value, string path = "")
		{
			List<string> errors = new List<string>();
			ValidateInternal(schema, value, path, errors);
			return errors;
		}
		private static string Problem(string path, string message)
		{
			return "`" + (path.Length == 0 ? "arguments" : path) + "`: " + message;
		}
		private static string Child(string path, string propertyName)
		{
			return path.Length == 0 ? propertyName : path + "." + propertyName;
		}
		private static void ValidateInternal(JObject schema, JToken value, string path, List<string> errors)
		{
			if (schema == null)
				return;
			if (value == null)
				value = JValue.CreateNull();

			JToken anyOf = schema["anyOf"] ?? schema["oneOf"];
			if (anyOf is JArray alternatives)
			{
				List<List<string>> results = alternatives.OfType<JObject>().Select(alt => Validate(alt, value, path)).ToList();
				int passed = results.Count(r => r.Count == 0);
				if (passed == 0)
				{
					// Report the errors of the alternative whose type matches, if any, since that is most likely what was intended.
					List<string> best = results.OrderBy(r => r.Count(e => e.Contains(": must be of type "))).ThenBy(r => r.Count).First();
					errors.AddRange(best);
					return;
				}
				if (schema["oneOf"] != null && passed > 1)
				{
					errors.Add(Problem(path, "matches more than one of the allowed forms."));
					return;
				}
			}

			JToken type = schema["type"];
			if (type != null)
			{
				string[] allowed = type.Type == JTokenType.Array ? type.Values<string>().ToArray() : new string[] { type.Value<string>() };
				if (!allowed.Any(t => IsType(value, t)))
				{
					errors.Add(Problem(path, "must be of type " + string.Join(" or ", allowed) + "."));
					return;
				}
			}

			if (schema["const"] != null && !JToken.DeepEquals(schema["const"], value))
				errors.Add(Problem(path, "must be " + schema["const"].ToString(Newtonsoft.Json.Formatting.None) + "."));

			if (schema["enum"] is JArray enumValues)
			{
				if (!enumValues.Any(e => JToken.DeepEquals(e, value)))
					errors.Add(Problem(path, "must be one of: " + string.Join(", ", enumValues.Select(e => e.ToString(Newtonsoft.Json.Formatting.None))) + "."));
			}

			if (value.Type == JTokenType.Integer || value.Type == JTokenType.Float)
			{
				double d = value.Value<double>();
				if (schema["minimum"] != null && d < schema["minimum"].Value<double>())
					errors.Add(Problem(path, "must be at least " + schema["minimum"] + "."));
				if (schema["maximum"] != null && d > schema["maximum"].Value<double>())
					errors.Add(Problem(path, "must be at most " + schema["maximum"] + "."));
			}
			if (value.Type == JTokenType.String)
			{
				string s = value.Value<string>();
				if (schema["minLength"] != null && s.Length < schema["minLength"].Value<int>())
					errors.Add(Problem(path, "must be at least " + schema["minLength"] + " characters long."));
				if (schema["maxLength"] != null && s.Length > schema["maxLength"].Value<int>())
					errors.Add(Problem(path, "must be at most " + schema["maxLength"] + " characters long."));
				else if (schema["pattern"] != null && !System.Text.RegularExpressions.Regex.IsMatch(s, schema["pattern"].Value<string>(), System.Text.RegularExpressions.RegexOptions.None, TimeSpan.FromMilliseconds(250)))
					errors.Add(Problem(path, "must match the pattern " + schema["pattern"] + "."));
			}
			if (value is JArray arr)
			{
				if (schema["minItems"] != null && arr.Count < schema["minItems"].Value<int>())
					errors.Add(Problem(path, "must have at least " + schema["minItems"] + " items."));
				if (schema["maxItems"] != null && arr.Count > schema["maxItems"].Value<int>())
					errors.Add(Problem(path, "must have at most " + schema["maxItems"] + " items."));
				if (schema["items"] is JObject itemSchema)
					for (int i = 0; i < arr.Count; i++)
						ValidateInternal(itemSchema, arr[i], path + "[" + i + "]", errors);
			}
			if (value is JObject obj)
			{
				JObject properties = schema["properties"] as JObject;
				if (schema["required"] is JArray required)
				{
					foreach (string name in required.Values<string>())
						if (obj[name] == null)
							errors.Add(Problem(Child(path, name), "is required."));
				}
				foreach (JProperty prop in obj.Properties())
				{
					JObject propSchema = properties?[prop.Name] as JObject;
					if (propSchema != null)
						ValidateInternal(propSchema, prop.Value, Child(path, prop.Name), errors);
					else if (schema["additionalProperties"] != null && schema["additionalProperties"].Type == JTokenType.Boolean && !schema["additionalProperties"].Value<bool>())
					{
						string known = properties == null ? "" : " Allowed: " + string.Join(", ", properties.Properties().Select(p => p.Name)) + ".";
						errors.Add(Problem(Child(path, prop.Name), "is not an allowed property." + known));
					}
				}
			}
		}
		private static bool IsType(JToken value, string type)
		{
			switch (type)
			{
				case "object": return value.Type == JTokenType.Object;
				case "array": return value.Type == JTokenType.Array;
				case "string": return value.Type == JTokenType.String;
				case "boolean": return value.Type == JTokenType.Boolean;
				case "null": return value.Type == JTokenType.Null;
				case "number": return value.Type == JTokenType.Integer || value.Type == JTokenType.Float;
				case "integer":
					if (value.Type == JTokenType.Integer)
						return true;
					if (value.Type == JTokenType.Float)
					{
						double d = value.Value<double>();
						return d == Math.Floor(d) && !double.IsInfinity(d) && Math.Abs(d) < 9007199254740992d;
					}
					return false;
				default: return false;
			}
		}
	}
}
