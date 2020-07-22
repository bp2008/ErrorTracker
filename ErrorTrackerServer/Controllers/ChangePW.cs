using BPUtil;
using BPUtil.MVC;
using ErrorTrackerServer.Database.Project.Model;
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
	public class ChangePW : UserController
	{
		public ActionResult StartChange()
		{
			if (session.authChallenge == null || session.authChallenge.Length == 0)
				session.authChallenge = ByteUtil.GenerateRandomBytes(32);

			return Json(new LoginChallengeResponse(session));
		}
		public ActionResult FinishChange()
		{
			ChangePasswordRequest request = ApiRequestBase.ParseRequest<ChangePasswordRequest>(this);

			byte[] challenge = session.authChallenge;
			if (challenge == null || challenge.Length == 0)
				return ApiError("Missing session state.  Please retry.");

			User user = session.GetUser();
			if (user.AuthenticateUser(request.oldPwToken, challenge))
			{
				byte[] newPwTokenBytes = Hex.ToByteArray(request.newPwToken);
				byte[] encryptionKey = Hash.GetSHA512Bytes(user.PasswordHash, challenge);
				byte[] decryptedNewPwHash = ByteUtil.XORByteArrays(encryptionKey, newPwTokenBytes);
				user.PasswordHash = Hash.GetSHA512Bytes(decryptedNewPwHash);
				Settings.data.Save();

				session.authChallenge = null;

				return Json(new ApiResponseBase(true));
			}
			else
				return ApiError("Old password was incorrect.");
		}
	}
	public class ChangePasswordRequest : ApiRequestBase
	{
		public string oldPwToken;
		public string newPwToken;
	}
	class LoginChallengeResponse : ApiResponseBase
	{
		/// <summary>
		/// A BCrypt salt string.
		/// </summary>
		public string salt;

		/// <summary>
		/// Random bytes (in hex format) which must be used in the authentication procedure.
		/// </summary>
		public string challenge;

		public LoginChallengeResponse(ServerSession session) : base(true)
		{
			this.salt = session.GetUser().Salt;
			this.challenge = ByteUtil.ToHex(session.authChallenge);
		}
	}
}
