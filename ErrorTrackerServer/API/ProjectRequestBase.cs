using BPUtil;
using BPUtil.MVC;
using BPUtil.SimpleHttp;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ErrorTrackerServer
{
	/// <summary>
	/// A base class for API Requests that defines a [projectName] parameter and provides a [Validate] method.
	/// </summary>
	public class ProjectRequestBase : ApiRequestBase
	{
		/// <summary>
		/// Project name provided by the client.
		/// </summary>
		public string projectName;

		/// <summary>
		/// <para>Validates the request from the client, returning true if okay and setting the [project] parameter.</para>
		/// <para>If the project is not found or is not allowed for the user, returns false and sets the [apiError] parameter.</para>
		/// </summary>
		/// <param name="needsWriteAccess">True if write access to the project is needed.</param>
		/// <param name="project">The Project</param>
		/// <param name="apiError">API Error, if the project is not found or is not allowed with the requested permission level.</param>
		/// <returns></returns>
		public bool Validate(bool needsWriteAccess, out Project project, out ApiResponseBase apiError)
		{
			return Validate(projectName, GetSession(), needsWriteAccess, out project, out apiError);
		}

		/// <summary>
		/// <para>Validates a project-based request from the client, returning true if okay and setting the [project] parameter.</para>
		/// <para>If the project is not found or is not allowed for the user, returns false and sets the [apiError] parameter.</para>
		/// </summary>
		/// <param name="projectName">Project Name</param>
		/// <param name="session">Session</param>
		/// <param name="needsWriteAccess">True if write access to the project is needed.</param>
		/// <param name="project">The Project</param>
		/// <param name="apiError">API Error, if the project is not found or is not allowed with the requested permission level.</param>
		/// <returns></returns>
		public static bool Validate(string projectName, ServerSession session, bool needsWriteAccess, out Project project, out ApiResponseBase apiError)
		{
			Project p = Settings.data.GetProject(projectName);
			if (p == null)
			{
				project = null;
				apiError = new ApiResponseBase(false, "Project \"" + projectName + "\" not found.");
				return false;
			}
			bool isAllowed = session.GetUser().IsProjectAllowed(p.Name);
			if (!isAllowed && !needsWriteAccess)
				isAllowed = session.GetUser().IsProjectAllowedReadOnly(p.Name);
			if (isAllowed)
			{
				project = p;
				apiError = null;
				return true;
			}
			else
			{
				project = null;
				apiError = new ApiResponseBase(false, "Project \"" + projectName + "\" not " + (needsWriteAccess ? "writable" : "readable") + " by your user account.");
				return false;
			}
		}
	}
}
