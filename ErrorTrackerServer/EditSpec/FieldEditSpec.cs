using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace ErrorTrackerServer
{
	/// <summary>
	/// Contains metadata about a data field for the purpose of creating a field-editing GUI.
	/// </summary>
	public class FieldEditSpec
	{
		/// <summary>
		/// HTML label.
		/// </summary>
		public string labelHtml;
		/// <summary>
		/// Name of the field (unique).
		/// </summary>
		public string key;
		/// <summary>
		/// Default value of the field.
		/// </summary>
		public object value;
		/// <summary>
		/// If not null/empty, this defines a set of allowed values which the client should enforce.
		/// </summary>
		public object[] allowedValues;
		/// <summary>
		/// HTML input type.
		/// </summary>
		public string inputType;
		/// <summary>
		/// HTML input type of array items.
		/// </summary>
		public string arrayType;
		/// <summary>
		/// Minimum value of the field.
		/// </summary>
		public object min;
		/// <summary>
		/// Maximum value of the field.
		/// </summary>
		public object max;
		/// <summary>
		/// Array of all possible enum values for this field.
		/// </summary>
		public string[] enumValues;
		public FieldEditSpec() { }
		public FieldEditSpec(FieldInfo f, object objWithDefaultValues)
		{
			labelHtml = f.Name;
			key = f.Name;
			value = f.GetValue(objWithDefaultValues);

			if (f.FieldType == typeof(string))
				inputType = "text";
			else if (IsNumberType(f.FieldType))
			{
				inputType = "number";
				min = f.FieldType.GetField("MinValue").GetValue(null);
				max = f.FieldType.GetField("MaxValue").GetValue(null);
			}
			else if (f.FieldType == typeof(bool))
				inputType = "checkbox";
			else if (f.FieldType.IsEnum)
			{
				// Only simple enum types are supported, where every acceptable value 
				// matches exactly one enum constant value.  No Flags should be used.
				// [value] will be a number that is the value's index in the enumValues array.
				inputType = "select";
				enumValues = f.FieldType.GetEnumNames();
			}
			else if (f.FieldType.IsArray)
			{
				inputType = "array";
				Type elementType = f.FieldType.GetElementType();
				if (elementType == typeof(string))
					arrayType = "text";
				else
					throw new Exception("EditSpec does not support arrays of type " + elementType);
			}
			else
				throw new Exception("EditSpec does not support type " + f.FieldType);
		}
		private static bool IsNumberType(Type t)
		{
			return t == typeof(sbyte) || t == typeof(byte)
				|| t == typeof(short) || t == typeof(ushort)
				|| t == typeof(int) || t == typeof(uint)
				|| t == typeof(long) || t == typeof(ulong)
				|| t == typeof(float) || t == typeof(double)
				|| t == typeof(decimal);
		}
	}
}
