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
	public class FilterData : UserController
	{
		public ActionResult GetAllFilters()
		{
			ProjectRequestBase request = ApiRequestBase.ParseRequest<ProjectRequestBase>(this);

			if (!request.Validate(out Project p, out ApiResponseBase error))
				return Json(error);

			GetAllFiltersResponse response = new GetAllFiltersResponse();
			using (DB db = new DB(p.Name))
				response.filters = db.GetAllFiltersSummary();
			return Json(response);
		}
		public ActionResult GetFilter()
		{
			OneFilterRequest request = ApiRequestBase.ParseRequest<OneFilterRequest>(this);

			if (!request.Validate(out Project p, out ApiResponseBase error))
				return Json(error);

			GetFilterResponse response = new GetFilterResponse();
			using (DB db = new DB(p.Name))
				response.filter = db.GetFilter(request.filterId);
			return Json(response);
		}
		public ActionResult AddFilter()
		{
			AddFilterRequest request = ApiRequestBase.ParseRequest<AddFilterRequest>(this);

			if (!request.Validate(out Project p, out ApiResponseBase error))
				return Json(error);

			using (DB db = new DB(p.Name))
			{
				int highestOrder = 0;
				foreach (FilterSummary sum in db.GetAllFiltersSummary())
					if (sum.filter.Order > highestOrder)
						highestOrder = sum.filter.Order;
				Filter newFilter = new Filter();
				newFilter.Name = request.name;
				newFilter.Order = highestOrder + 1;
				if (db.AddFilter(newFilter, out string errorMessage))
				{
					Logger.Info("Filter " + newFilter.FilterId + " (\"" + newFilter.Name + "\") was added by \"" + session.userName + "\"");
					return Json(new ApiResponseBase(true));
				}
				else
					return ApiError(errorMessage);
			}
		}
		/// <summary>
		/// Updates a filter and all attached conditions and actions.  This method cannot be used to add or remove conditions or actions, or to update filter properties separately from conditions and actions.
		/// </summary>
		/// <returns></returns>
		public ActionResult EditFilter()
		{
			EditFilterRequest request = ApiRequestBase.ParseRequest<EditFilterRequest>(this);

			if (!request.Validate(out Project p, out ApiResponseBase error))
				return Json(error);

			using (DB db = new DB(p.Name))
			{
				if (db.EditFilter(request.filter, out string errorMessage))
				{
					Logger.Info("Filter " + request.filter.filter.FilterId + " (\"" + request.filter.filter.Name + "\") was edited by \"" + session.userName + "\"");
					return Json(new ApiResponseBase(true));
				}
				else
					return ApiError(errorMessage);
			}
		}
		public ActionResult DeleteFilter()
		{
			OneFilterRequest request = ApiRequestBase.ParseRequest<OneFilterRequest>(this);

			if (!request.Validate(out Project p, out ApiResponseBase error))
				return Json(error);

			using (DB db = new DB(p.Name))
			{
				if (db.DeleteFilter(request.filterId))
				{
					Logger.Info("Filter " + request.filterId + " was deleted by \"" + session.userName + "\"");
					return Json(new ApiResponseBase(true));
				}
				else
					return ApiError("Unable to delete filter " + request.filterId);
			}
		}
		public ActionResult AddCondition()
		{
			OneFilterConditionRequest request = ApiRequestBase.ParseRequest<OneFilterConditionRequest>(this);

			if (!request.Validate(out Project p, out ApiResponseBase error))
				return Json(error);

			using (DB db = new DB(p.Name))
			{
				if (db.AddFilterCondition(request.condition, out string errorMessage))
				{
					Logger.Info("Filter condition" + request.condition.FilterConditionId + " was added by \"" + session.userName + "\"");
					return Json(new ApiResponseBase(true));
				}
				else
					return ApiError(errorMessage);
			}
		}
		public ActionResult EditCondition()
		{
			OneFilterConditionRequest request = ApiRequestBase.ParseRequest<OneFilterConditionRequest>(this);

			if (!request.Validate(out Project p, out ApiResponseBase error))
				return Json(error);

			using (DB db = new DB(p.Name))
			{
				if (db.EditFilterCondition(request.condition, out string errorMessage))
				{
					Logger.Info("Filter condition" + request.condition.FilterConditionId + " was edited by \"" + session.userName + "\"");
					return Json(new ApiResponseBase(true));
				}
				else
					return ApiError(errorMessage);
			}
		}
		public ActionResult DeleteCondition()
		{
			OneFilterConditionRequest request = ApiRequestBase.ParseRequest<OneFilterConditionRequest>(this);

			if (!request.Validate(out Project p, out ApiResponseBase error))
				return Json(error);

			using (DB db = new DB(p.Name))
			{
				if (db.DeleteFilterCondition(request.condition.FilterConditionId))
				{
					Logger.Info("Filter condition" + request.condition.FilterConditionId + " was deleted by \"" + session.userName + "\"");
					return Json(new ApiResponseBase(true));
				}
				else
					return ApiError("Unable to delete filter condition " + request.condition.FilterConditionId);
			}
		}
		public ActionResult AddAction()
		{
			OneFilterActionRequest request = ApiRequestBase.ParseRequest<OneFilterActionRequest>(this);

			if (!request.Validate(out Project p, out ApiResponseBase error))
				return Json(error);

			using (DB db = new DB(p.Name))
			{
				if (db.AddFilterAction(request.action, out string errorMessage))
				{
					Logger.Info("Filter condition" + request.action.FilterActionId + " was added by \"" + session.userName + "\"");
					return Json(new ApiResponseBase(true));
				}
				else
					return ApiError(errorMessage);
			}
		}
		public ActionResult EditAction()
		{
			OneFilterActionRequest request = ApiRequestBase.ParseRequest<OneFilterActionRequest>(this);

			if (!request.Validate(out Project p, out ApiResponseBase error))
				return Json(error);

			using (DB db = new DB(p.Name))
			{
				if (db.EditFilterAction(request.action, out string errorMessage))
				{
					Logger.Info("Filter condition" + request.action.FilterActionId + " was edited by \"" + session.userName + "\"");
					return Json(new ApiResponseBase(true));
				}
				else
					return ApiError(errorMessage);
			}
		}
		public ActionResult DeleteAction()
		{
			OneFilterActionRequest request = ApiRequestBase.ParseRequest<OneFilterActionRequest>(this);

			if (!request.Validate(out Project p, out ApiResponseBase error))
				return Json(error);

			using (DB db = new DB(p.Name))
			{
				if (db.DeleteFilterAction(request.action.FilterActionId))
				{
					Logger.Info("Filter condition" + request.action.FilterActionId + " was deleted by \"" + session.userName + "\"");
					return Json(new ApiResponseBase(true));
				}
				else
					return ApiError("Unable to delete filter action " + request.action.FilterActionId);
			}
		}
		public ActionResult ReorderFilters()
		{
			ReorderFiltersRequest request = ApiRequestBase.ParseRequest<ReorderFiltersRequest>(this);

			if (!request.Validate(out Project p, out ApiResponseBase error))
				return Json(error);

			using (DB db = new DB(p.Name))
			{
				db.ReorderFilters(request.newOrder);
			}
			Logger.Info("Filters reordered by \"" + session.userName + "\"");
			return Json(new ApiResponseBase(true));
		}
		public ActionResult RunFilterAgainstAllEvents()
		{
			OneFilterRequest request = ApiRequestBase.ParseRequest<OneFilterRequest>(this);

			if (!request.Validate(out Project p, out ApiResponseBase error))
				return Json(error);

			using (FilterEngine fe = new FilterEngine(request.projectName))
				fe.RunFilterAgainstAllEvents(request.filterId, true);
			return Json(new ApiResponseBase(true));
		}
		public ActionResult RunEnabledFiltersAgainstAllEvents()
		{
			ProjectRequestBase request = ApiRequestBase.ParseRequest<ProjectRequestBase>(this);

			if (!request.Validate(out Project p, out ApiResponseBase error))
				return Json(error);

			using (FilterEngine fe = new FilterEngine(request.projectName))
				fe.RunEnabledFiltersAgainstAllEvents();
			return Json(new ApiResponseBase(true));
		}
	}
	public class GetAllFiltersResponse : ApiResponseBase
	{
		public List<FilterSummary> filters;
		public GetAllFiltersResponse() : base(true, null) { }
	}
	public class GetFilterResponse : ApiResponseBase
	{
		public FullFilter filter;
		public GetFilterResponse() : base(true, null) { }
	}
	public class AddFilterRequest : ProjectRequestBase
	{
		public string name;
	}
	public class EditFilterRequest : ProjectRequestBase
	{
		public FullFilter filter;
	}
	public class OneFilterRequest : ProjectRequestBase
	{
		public int filterId;
	}
	public class OneFilterConditionRequest : ProjectRequestBase
	{
		public FilterCondition condition;
	}
	public class OneFilterActionRequest : ProjectRequestBase
	{
		public FilterAction action;
	}
	public class ReorderFiltersRequest : ProjectRequestBase
	{
		public FilterOrder[] newOrder;
	}
}
