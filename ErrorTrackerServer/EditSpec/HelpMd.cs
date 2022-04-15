using System;

namespace ErrorTrackerServer
{
	/// <summary>
	/// Adds Markdown help text to a member, for use in the <see cref="FieldEditSpec"/> system.
	/// </summary>
	public class HelpMd : Attribute
	{
		public string Markdown;
		/// <summary>
		/// Apply to a class member to associate this help text with the member.
		/// </summary>
		/// <param name="md">Markdown that can be rendered as help text to describe the annotated member.</param>
		public HelpMd(string md)
		{
			Markdown = md;
		}
	}
}