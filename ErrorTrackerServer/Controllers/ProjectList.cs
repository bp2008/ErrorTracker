using BPUtil;
using BPUtil.MVC;
using Newtonsoft.Json;
using System;
using System.Collections.Concurrent;
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
				.Select(p => new ClientProject(p.Name))
				.OrderBy(p => p.Name)
				.ToList();
			return Json(response);
		}
		/// <summary>
		/// Returns a list of project names available to the current user along with event count and unique event count. This call is slow for large databases.
		/// </summary>
		/// <returns></returns>
		public ActionResult GetProjectEventCounts()
		{
			BasicEventTimer bet = new BasicEventTimer();
			bet.Start("Startup");
			User u = session.GetUser();
			List<Project> projects = Settings.data.GetAllProjects()
				.Where(p => u.IsProjectAllowed(p.Name) || u.IsProjectAllowedReadOnly(p.Name))
				.ToList();

			ConcurrentDictionary<string, ClientProject> dictProjects = new ConcurrentDictionary<string, ClientProject>();
			ParallelOptions parallelOptions = new ParallelOptions();
			parallelOptions.MaxDegreeOfParallelism = projects.Count;

			bet.Start("Count Events (parallel)");
			Parallel.ForEach(projects, parallelOptions, p =>
			{
				ClientProject proj = new ClientProject(p.Name);
				using (DB db = new DB(p.Name))
				{
					proj.EventCount = db.CountEvents();
					proj.UniqueEventCount = db.CountUniqueEvents();
				}
				dictProjects[p.Name] = proj;
			});

			bet.Start("Sort and finish");
			GetProjectListResponse response = new GetProjectListResponse();
			response.projects = dictProjects.Values
				.OrderBy(p => p.Name)
				.ToList();
			bet.Stop();

			Context.ResponseHeaders["Server-Timing"] = bet.ToServerTimingHeader();
			return Json(response);
		}

		//private static ConcurrentDictionary<string, CachedObject<long>> cacheOfUniqueEventCounts = new ConcurrentDictionary<string, CachedObject<long>>();
		//private static long GetUniqueEventCount(string projectName)
		//{
		//	return cacheOfUniqueEventCounts.GetOrAdd(projectName, CacheBuildUniqueEventCount).GetInstance();
		//}
		//private static CachedObject<long> CacheBuildUniqueEventCount(string projectName)
		//{
		//	return new CachedObject<long>(() =>
		//		{
		//			using (DB db = new DB(projectName))
		//			{
		//				return db.CountUniqueEvents();
		//			}
		//		}
		//		, TimeSpan.FromSeconds(10)
		//		, TimeSpan.FromDays(1)
		//		, ex => Emailer.SendError("CountUniqueEvents failed: " + ex.ToHierarchicalString(), false));
		//}
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
