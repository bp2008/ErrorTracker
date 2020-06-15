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
	public class UserData : AdminController
	{
		public ActionResult GetUserData()
		{
			GetUserDataResponse response = new GetUserDataResponse();
			return Json(response);
		}
		public ActionResult SetUserData()
		{
			SetUserDataRequest request = ApiRequestBase.ParseRequest<SetUserDataRequest>(this);

			User u = Settings.data.GetUser(request.userName);
			if (u == null)
				return ApiError("The specified user was not found.");

			if (!ValidateUserName(request.userData.Name))
				return Json(new ApiResponseBase(false, "user name is invalid"));
			if (request.userData.AllowedProjects == null)
				request.userData.AllowedProjects = new string[0];

			u.Name = request.userData.Name;
			u.Email = request.userData.Email;
			if (!string.IsNullOrWhiteSpace(request.userData.SetPassword))
				u.SetPassword(request.userData.SetPassword);
			if (!u.Permanent)
				u.IsAdmin = request.userData.IsAdmin;
			u.SetAllowedProjects(request.userData.AllowedProjects.ToList());
			Settings.data.Save();

			Logger.Info("User \"" + request.userName + "\" was edited by \"" + session.userName + "\"");

			return Json(new ApiResponseBase(true));
		}
		public ActionResult AddUser()
		{
			AddUserRequest request = ApiRequestBase.ParseRequest<AddUserRequest>(this);

			if (!ValidateUserName(request.userData.Name))
				return Json(new ApiResponseBase(false, "user name is invalid"));
			if (string.IsNullOrWhiteSpace(request.userData.SetPassword))
				return Json(new ApiResponseBase(false, "password is invalid"));
			if (request.userData.AllowedProjects == null)
				request.userData.AllowedProjects = new string[0];

			User user = new User(request.userData.Name, request.userData.SetPassword, request.userData.Email, request.userData.IsAdmin);
			user.SetAllowedProjects(request.userData.AllowedProjects.ToList());

			if (Settings.data.TryAddUser(user))
			{
				Settings.data.Save();
				Logger.Info("User \"" + user.Name + "\" was added by \"" + session.userName + "\"");
				return Json(new ApiResponseBase(true));
			}
			else
				return Json(new ApiResponseBase(false, "user name is already taken"));
		}
		public ActionResult RemoveUser()
		{
			RemoveUserRequest request = ApiRequestBase.ParseRequest<RemoveUserRequest>(this);
			User u = Settings.data.GetUser(request.userName);
			if (u == null)
				return Json(new ApiResponseBase(false, "user could not be found"));
			if (u.Permanent)
				return Json(new ApiResponseBase(false, "User could not be removed. User is permanent."));

			if (Settings.data.TryRemoveUser(request.userName) != null)
			{
				Settings.data.Save();
				Logger.Info("User \"" + request.userName + "\" was deleted by \"" + session.userName + "\"");
				return Json(new ApiResponseBase(true));
			}
			else
				return Json(new ApiResponseBase(false, "user could not be found"));
		}
		private static bool ValidateUserName(string userName)
		{
			return !string.IsNullOrWhiteSpace(userName) && StringUtil.IsAlphaNumericOrUnderscore(userName);
		}
	}
	public class GetUserDataResponse : ApiResponseBase
	{
		/// <summary>
		/// A list containing the current users.
		/// </summary>
		public List<ProjectDataObject> users;
		/// <summary>
		/// Contains metadata about user data fields for the purpose of creating a field-editing GUI.
		/// </summary>
		public List<FieldEditSpec> editSpec;
		public GetUserDataResponse() : base(true, null)
		{
			users = Settings.data.GetAllUsers().Select(u => new ProjectDataObject(u)).ToList();
			ProjectDataObject uoDefault = new ProjectDataObject(new User());
			editSpec = uoDefault.GetType().GetFields().Select(f =>
			{
				return new FieldEditSpec(f, uoDefault);
			}).ToList();
			FieldEditSpec allowedProjects = editSpec.First(s => s.key == "AllowedProjects");
			allowedProjects.allowedValues = Settings.data.GetAllProjects().Select(p=>p.Name).ToArray();
		}
	}
	public class SetUserDataRequest : ApiRequestBase
	{
		/// <summary>
		/// User name of the user to modify.
		/// </summary>
		public string userName;
		/// <summary>
		/// An object containing the new user data.
		/// </summary>
		public ProjectDataObject userData;
	}
	public class AddUserRequest : ApiRequestBase
	{
		/// <summary>
		/// An object containing the new user data.
		/// </summary>
		public ProjectDataObject userData;
	}
	public class RemoveUserRequest : ApiRequestBase
	{
		/// <summary>
		/// User name of the user to remove.
		/// </summary>
		public string userName;
	}

	public class ProjectDataObject
	{
		public string Name;
		public string Email;
		public string SetPassword;
		public bool IsAdmin = false;
		public string[] AllowedProjects;
		public ProjectDataObject() { }
		public ProjectDataObject(User u)
		{
			Name = u.Name;
			Email = u.Email;
			IsAdmin = u.IsAdmin;
			AllowedProjects = u.GetAllowedProjects().ToArray();
		}
	}
}
