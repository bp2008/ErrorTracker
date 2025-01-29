using BPUtil;
using BPUtil.MVC;
using ErrorTrackerServer.Database.Global;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ErrorTrackerServer.Controllers
{
	public class Auth : ETController
	{
		#region Login
		public ActionResult Login()
		{
			LoginRequest args = ApiRequestBase.ParseRequest<LoginRequest>(this);

			ServerSession session = args.GetSession();

			bool isNewSession = false;
			if (session == null)
			{
				isNewSession = true;
				session = ServerSession.CreateUnauthenticated();
			}

			// Initialize challenge data
			if (session.authChallenge == null || session.authChallenge.Length == 0)
				session.authChallenge = ByteUtil.GenerateRandomBytes(32);

			// Get user
			if (string.IsNullOrEmpty(args.user))
				return ApiError("missing parameter: \"user\"");
			User user = Settings.data.GetUser(args.user);

			string salt;
			if (user != null)
				salt = user.Salt;
			else
			{
				salt = Util.GenerateFakeUserSalt(args.user);
				Logger.Info("Fake salt \"" + salt + "\" created for user name \"" + args.user + "\". Remote IP: " + Context.httpProcessor.RemoteIPAddressStr);
			}

			if (string.IsNullOrEmpty(args.token))
			{
				// No response token was provided, so we are on step 1 of authentication, where the client requests information necessary to build the response token.
				return Json(new LoginChallengeResponse(session, salt, null));
			}
			else
			{
				// A response token was provided. This token can be used to authenticate the user.
				if (user != null)
				{
					if (user.AuthenticateUser(args.token, session.authChallenge))
					{
						// Delete challenge data -- it is no longer needed and should not be reused.
						session.UserHasAuthenticated(user.Name);
						using (GlobalDb db = new GlobalDb())
							db.AddLoginRecord(user.Name, Context.httpProcessor.RemoteIPAddressStr, session.sid);
						return Json(new LoginSuccessResponse(session));
					}
				}
				if (isNewSession)
					return Json(new LoginFailedResponse(session, "Authentication protocol timed out. Please try again."));
				return Json(new LoginFailedResponse(session, "Authentication failed. Please check inputs."));
			}
		}
		class AuthResponseBase : ApiResponseBase
		{
			public string sid;
			public AuthResponseBase(ServerSession session, bool success, string error = null) : base(success, error)
			{
				this.sid = session.sid;
			}
		}
#pragma warning disable CS0649
		class LoginRequest : ApiRequestBase
		{
			/// <summary>
			/// User Name
			/// </summary>
			public string user;
			/// <summary>
			/// Cryptographic token to prove ownership of the specified account. This is normally omitted during the first step of login so that a login challenge response can be generated.
			/// </summary>
			public string token;
		}
#pragma warning restore CS0649
		class LoginChallengeResponse : AuthResponseBase
		{
			/// <summary>
			/// Always false to indicate that authentication is not successful (yet).
			/// </summary>
			public readonly bool authenticated = false;

			/// <summary>
			/// A BCrypt salt string.
			/// </summary>
			public string salt;

			/// <summary>
			/// Random bytes (in hex format) which must be used in the authentication procedure.
			/// </summary>
			public string challenge;

			/// <summary>
			/// A message to show to the user. May be null/missing.
			/// </summary>
			[JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
			public string message;

			public LoginChallengeResponse(ServerSession session, string salt, string message) : base(session, true)
			{
				this.salt = salt;
				this.challenge = ByteUtil.ToHex(session.authChallenge);
				this.message = message;
			}
		}
		class LoginFailedResponse : AuthResponseBase
		{
			/// <summary>
			/// Always false to indicate that authentication is not successful (yet).
			/// </summary>
			public readonly bool authenticated = false;

			/// <summary>
			/// A message to show to the user. May be null/missing.
			/// </summary>
			[JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
			public string message;

			public LoginFailedResponse(ServerSession session, string message) : base(session, true)
			{
				this.message = message;
			}
		}
		class LoginSuccessResponse : AuthResponseBase
		{
			/// <summary>
			/// Always true to indicate that authentication was successful.
			/// </summary>
			public readonly bool authenticated = true;
			/// <summary>
			/// Indicates if the user has admin privilege.
			/// </summary>
			public readonly bool isAdmin = true;

			public LoginSuccessResponse(ServerSession session) : base(session, true)
			{
				isAdmin = session.IsAdminValid;
			}
		}
		#endregion
		public ActionResult Logout()
		{
			ApiRequestBase args = ApiRequestBase.ParseRequest<ApiRequestBase>(this);
			SessionManager.RemoveSession(args.sid);
			return Json(new ApiResponseBase(true));
		}
	}
}
