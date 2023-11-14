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
				.Where(p => u.IsProjectAllowed(p.Name) || u.IsProjectAllowedReadOnly(p.Name))
				.Select(p =>
				{
					ClientProject proj = new ClientProject(p.Name);
					using (DB db = new DB(p.Name))
					{
						proj.EventCount = db.CountEvents();
						proj.UniqueEventCount = db.CountUniqueEvents();
					}
					return proj;
				})
				.ToList();
			return Json(response);
		}
	}
	public class GetProjectListResponse : ApiResponseBase
	{
		/// <summary>
		/// A list containing the current projects.
		/// </summary>
		public List<ClientProject> projects;
		public GetProjectListResponse() : base(true, null) { }
	}

	public class ClientProject
	{
		public string Name;
		public long EventCount;
		public long UniqueEventCount;
		public ClientProject() { }
		public ClientProject(string Name)
		{
			this.Name = Name;
		}
	}
}
