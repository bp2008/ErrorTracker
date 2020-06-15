using BPUtil.MVC;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ErrorTrackerServer.Controllers
{
	/// <summary>
	/// A base class for an API controller that requires a user session to access.
	/// </summary>
	public abstract class UserController : ETController
	{
		/// <summary>
		/// The user's session, guaranteed to be authorized and active at the time of controller loading.
		/// </summary>
		protected ServerSession session;

		/// <summary>
		/// May allow or disallow access to the controller.  This is called before the client-specified action method is called.
		/// </summary>
		/// <param name="result">If authorization fails, this should be set to an appropriate result such as an HTTP 403 Forbidden response. If null, authorization will be assumed to have succeeded.</param>
		public override void OnAuthorization(ref ActionResult result)
		{
			base.OnAuthorization(ref result);
			if (result != null)
				return;
			ApiRequestBase args = ApiRequestBase.ParseRequest<ApiRequestBase>(this);
			session = args.GetSession();
			if (session == null)
				result = StatusCode("403 Forbidden");
			else if (!session.IsAuthValid)
				result = StatusCode("418 Insufficient Privilege");
		}
	}
}
