using BPUtil;
using BPUtil.MVC;
using ErrorTrackerServer.Database.Model;
using ErrorTrackerServer.Filtering;
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
	public class FolderData : UserController
	{
		public ActionResult GetAllFolders()
		{
			ProjectRequestBase request = ApiRequestBase.ParseRequest<ProjectRequestBase>(this);

			if (!request.Validate(out Project p, out ApiResponseBase error))
				return Json(error);

			GetAllFoldersResponse response = new GetAllFoldersResponse();
			using (DB db = new DB(p.Name))
				response.folders = db.GetAllFolders();
			return Json(response);
		}
		public ActionResult GetFolderStructure()
		{
			ProjectRequestBase request = ApiRequestBase.ParseRequest<ProjectRequestBase>(this);

			if (!request.Validate(out Project p, out ApiResponseBase error))
				return Json(error);

			GetFolderStructureResponse response = new GetFolderStructureResponse();
			using (DB db = new DB(p.Name))
				response.root = db.GetFolderStructure();
			return Json(response);
		}
		public ActionResult AddFolder()
		{
			AddFolderRequest request = ApiRequestBase.ParseRequest<AddFolderRequest>(this);

			if (!request.Validate(out Project p, out ApiResponseBase error))
				return Json(error);

			using (DB db = new DB(p.Name))
			{
				if (db.AddFolder(request.folderName, request.parentFolderId, out string errorMessage))
					return Json(new ApiResponseBase(true));
				else
					return ApiError(errorMessage);
			}
		}
		public ActionResult MoveFolder()
		{
			MoveFolderRequest request = ApiRequestBase.ParseRequest<MoveFolderRequest>(this);

			if (!request.Validate(out Project p, out ApiResponseBase error))
				return Json(error);

			using (DB db = new DB(p.Name))
			{
				if (db.MoveFolder(request.folderId, request.newParentFolderId, out string errorMessage))
					return Json(new ApiResponseBase(true));
				else
					return ApiError(errorMessage);
			}
		}
		public ActionResult RenameFolder()
		{
			RenameFolderRequest request = ApiRequestBase.ParseRequest<RenameFolderRequest>(this);

			if (!request.Validate(out Project p, out ApiResponseBase error))
				return Json(error);

			using (DB db = new DB(p.Name))
			{
				if (db.RenameFolder(request.folderId, request.newFolderName, out string errorMessage))
					return Json(new ApiResponseBase(true));
				else
					return ApiError(errorMessage);
			}
		}
		public ActionResult DeleteFolder()
		{
			DeleteFolderRequest request = ApiRequestBase.ParseRequest<DeleteFolderRequest>(this);

			if (!request.Validate(out Project p, out ApiResponseBase error))
				return Json(error);

			using (DB db = new DB(p.Name))
			{
				if (db.DeleteFolder(request.folderId, out string errorMessage))
					return Json(new ApiResponseBase(true));
				else
					return ApiError(errorMessage);
			}
		}
		public ActionResult RunFilterOnFolder()
		{
			RunFilterOnFolderRequest request = ApiRequestBase.ParseRequest<RunFilterOnFolderRequest>(this);

			if (!request.Validate(out Project p, out ApiResponseBase error))
				return Json(error);

			try
			{
				using (FilterEngine fe = new FilterEngine(request.projectName))
					fe.RunFilterAgainstFolder(request.filterId, request.folderId, true);
			}
			catch (Exception ex)
			{
				return ApiError(ex.ToString());
			}
			return Json(new ApiResponseBase(true));
		}
	}
	public class GetAllFoldersResponse : ApiResponseBase
	{
		public List<Folder> folders;
		public GetAllFoldersResponse() : base(true, null) { }
	}
	public class GetFolderStructureResponse : ApiResponseBase
	{
		public FolderStructure root;
		public GetFolderStructureResponse() : base(true, null) { }
	}
	public class AddFolderRequest : ProjectRequestBase
	{
		public string folderName;
		public int parentFolderId;
	}
	public class MoveFolderRequest : ProjectRequestBase
	{
		public int folderId;
		public int newParentFolderId;
	}
	public class RenameFolderRequest : ProjectRequestBase
	{
		public int folderId;
		public string newFolderName;
	}
	public class DeleteFolderRequest : ProjectRequestBase
	{
		public int folderId;
	}
	public class RunFilterOnFolderRequest : ProjectRequestBase
	{
		public int folderId;
		public int filterId;
	}
}
