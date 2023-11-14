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
	public class PushData : UserController
	{
		public ActionResult GetVapidPublicKey()
		{
			VapidPublicKeyResponse response = new VapidPublicKeyResponse(Settings.data.vapidPublicKey);
			return Json(response);
		}
		public ActionResult RegisterForPush()
		{
			PushRegistrationRequest request = ApiRequestBase.ParseRequest<PushRegistrationRequest>(this);

			if (!request.Validate(false, out Project p, out ApiResponseBase error))
				return Json(error);

			User user = session.GetUser();
			user.SetPushNotificationSubscription(request.projectName, request.folderId, request.subscriptionKey, true);
			Settings.data.Save();

			return Json(new ApiResponseBase(true));
		}
		public ActionResult UnregisterForPush()
		{
			PushRegistrationRequest request = ApiRequestBase.ParseRequest<PushRegistrationRequest>(this);

			if (!request.Validate(false, out Project p, out ApiResponseBase error))
				return Json(error);

			User user = session.GetUser();
			user.SetPushNotificationSubscription(request.projectName, request.folderId, request.subscriptionKey, false);
			Settings.data.Save();

			return Json(new ApiResponseBase(true));
		}
		public ActionResult GetRegistrationStatus()
		{
			PushRegistrationRequest request = ApiRequestBase.ParseRequest<PushRegistrationRequest>(this);

			if (!request.Validate(false, out Project p, out ApiResponseBase error))
				return Json(error);

			User user = session.GetUser();
			string[] keys = user.GetPushNotificationSubscriptions(request.projectName, request.folderId);
			bool subscribed = keys.Contains(request.subscriptionKey);
			return Json(new GetRegistrationStatusResponse(subscribed));
		}
	}
	public class VapidPublicKeyResponse : ApiResponseBase
	{
		public string vapidPublicKey;
		public VapidPublicKeyResponse(string vapidPublicKey) : base(true, null)
		{
			this.vapidPublicKey = vapidPublicKey;
		}
	}
	public class PushRegistrationRequest : ProjectRequestBase
	{
		public int folderId;
		public string subscriptionKey;
	}
	public class GetRegistrationStatusResponse : ApiResponseBase
	{
		public bool subscribed;
		public GetRegistrationStatusResponse(bool subscribed) : base(true, null)
		{
			this.subscribed = subscribed;
		}
	}
}
