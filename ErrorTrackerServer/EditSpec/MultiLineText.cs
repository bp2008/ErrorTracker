using System;

namespace ErrorTrackerServer
{
	/// <summary>
	/// Marks a string member as multi-line so that the <see cref="FieldEditSpec"/> system presents a textarea
	/// instead of a single-line text box.
	/// </summary>
	[AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
	public class MultiLineText : Attribute
	{
		/// <summary>
		/// Number of visible text rows the editor should give the field.
		/// </summary>
		public int Rows;
		/// <summary>
		/// Marks a string member as multi-line.
		/// </summary>
		/// <param name="rows">Number of visible text rows the editor should give the field.</param>
		public MultiLineText(int rows = 6)
		{
			Rows = rows;
		}
	}
}
