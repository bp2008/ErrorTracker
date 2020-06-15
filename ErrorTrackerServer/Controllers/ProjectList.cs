using BPUtil;
using BPUtil.MVC;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ErrorTrackerServer.Controllers
{
	public class ProjectList : UserController
	{
		/// <summary>
		/// Returns a list of project names available to the current user.
		/// </summary>
		/// <returns></returns>
		public ActionResult GetProjectList()
		{
			User u = session.GetUser();
			GetProjectListResponse response = new GetProjectListResponse();
			response.projects = Settings.data.GetAllProjects()
				.Where(p => u.IsProjectAllowed(p.Name))
				.Select(p => p.Name)
				.ToList();
			return Json(response);
		}
	}
	public class GetProjectListResponse : ApiResponseBase
	{
		/// <summary>
		/// A list containing the current projects.
		/// </summary>
		public List<string> projects;
		public GetProjectListResponse() : base(true, null) { }
	}
}
