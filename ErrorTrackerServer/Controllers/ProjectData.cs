﻿using BPUtil;
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
	public class ProjectData : AdminController
	{
		public ActionResult GetProjectData()
		{
			GetProjectDataResponse response = new GetProjectDataResponse();
			return Json(response);
		}
		public ActionResult AddProject()
		{
			ProjectRequest request = ApiRequestBase.ParseRequest<ProjectRequest>(this);

			if (string.IsNullOrWhiteSpace(request.projectName) || !StringUtil.IsAlphaNumericOrUnderscore(request.projectName))
				return Json(new ApiResponseBase(false, "project name is invalid"));

			request.projectName = request.projectName.Trim();
			if (request.projectName.Length > 64)
				return Json(new ApiResponseBase(false, "project name is too long. Max length: 64 characters."));

			Project p = new Project();
			p.Name = request.projectName;
			p.InitializeSubmitKey();
			if (Settings.data.TryAddProject(p))
			{
				Settings.data.Save();
				Logger.Info("Project \"" + request.projectName + "\" was added by \"" + session.userName + "\"");
				return Json(new ApiResponseBase(true));
			}
			else
				return Json(new ApiResponseBase(false, "project name is already taken"));
		}
		public ActionResult RemoveProject()
		{
			ProjectRequest request = ApiRequestBase.ParseRequest<ProjectRequest>(this);
			Project p = Settings.data.GetProject(request.projectName);
			if (p == null)
				return Json(new ApiResponseBase(false, "project could not be found"));

			if (Settings.data.TryRemoveProject(request.projectName) != null)
			{
				Settings.data.Save();
				Logger.Info("Project \"" + request.projectName + "\" was deleted by \"" + session.userName + "\"");
				return Json(new ApiResponseBase(true));
			}
			else
				return Json(new ApiResponseBase(false, "project could not be found"));
		}
		public ActionResult ReplaceSubmitKey()
		{
			ProjectRequest request = ApiRequestBase.ParseRequest<ProjectRequest>(this);

			Project p = Settings.data.GetProject(request.projectName);
			if (p == null)
				return Json(new ApiResponseBase(false, "project could not be found"));

			p.InitializeSubmitKey();
			Settings.data.Save();

			Logger.Info("Project \"" + request.projectName + "\" had its submit key replaced by \"" + session.userName + "\"");
			return Json(new ApiResponseBase(true));
		}
		public ActionResult UpdateProject()
		{
			UpdateProjectRequest request = ApiRequestBase.ParseRequest<UpdateProjectRequest>(this);

			Project p = Settings.data.GetProject(request.projectName);
			if (p == null)
				return Json(new ApiResponseBase(false, "project could not be found"));

			p.MaxEventAgeDays = request.MaxEventAgeDays;
			if (request.CloneTo == null)
				request.CloneTo = new string[0];
			p.CloneTo = request.CloneTo;

			Settings.data.Save();

			Logger.Info("Project \"" + request.projectName + "\" was updated by \"" + session.userName + "\". " + JsonConvert.SerializeObject(p));
			return Json(new ApiResponseBase(true));
		}
	}
	public class GetProjectDataResponse : ApiResponseBase
	{
		/// <summary>
		/// A list containing the current projects.
		/// </summary>
		public List<ProjectInfo> projects;
		public GetProjectDataResponse() : base(true, null)
		{
			List<Project> allProjects = Settings.data.GetAllProjects();
			IEnumerable<string> allNames = allProjects.Select(p => p.Name);
			projects = allProjects
				.Select(p =>
				{
					return new ProjectInfo()
					{
						Name = p.Name,
						SubmitKey = p.SubmitKey,
						MaxEventAgeDays = p.MaxEventAgeDays,
						CloneTo = p.CloneTo,
						CloneToEditSpec = new FieldEditSpec(typeof(Project).GetField("CloneTo"), new Project())
						{
							labelHtml = "Clone New Events to Other Projects: ",
							allowedValues = allNames.Where(n => n != p.Name).ToArray()
						}
					};
				})
				.ToList();
		}
	}
	public class ProjectInfo
	{
		public string Name;
		public string SubmitKey;
		public int MaxEventAgeDays;
		public string[] CloneTo;
		public FieldEditSpec CloneToEditSpec;
	}
	public class ProjectRequest : ApiRequestBase
	{
		/// <summary>
		/// Project name of the project being referred to.
		/// </summary>
		public string projectName;
	}
	public class UpdateProjectRequest : ProjectRequest
	{
		public int MaxEventAgeDays;
		public string[] CloneTo;
	}
}
