using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using BPUtil;
using BPUtil.PasswordReset;

namespace ErrorTrackerServer
{
	/// <summary>
	/// Allows the stateless password reset system to access accounts from the database.
	/// </summary>
	public class ErrorTrackerPasswordReset : StatelessPasswordResetBase
	{
		public ErrorTrackerPasswordReset() : base("ErrorTrackerUser")
		{
		}

		protected override AccountInfo GetCurrentAccountInfo(string accountIdentifier)
		{
			User user = Settings.data.GetUser(accountIdentifier);
			if (user == null || string.IsNullOrWhiteSpace(user.Email))
				return null;
			AccountInfo accountInfo = new AccountInfo();
			accountInfo.Identifier = user.Name;
			accountInfo.Email = user.Email;
			accountInfo.DisplayName = user.Name;
			accountInfo.Password = Convert.ToBase64String(user.PasswordHash);
			return accountInfo;
		}

		protected override bool CommitPasswordChange(string accountIdentifier, string newPassword)
		{
			User user = Settings.data.GetUser(accountIdentifier);
			if (user == null || string.IsNullOrWhiteSpace(user.Email) || string.IsNullOrWhiteSpace(newPassword))
				return false;

			user.SetPassword(newPassword);
			Settings.data.Save();

			return true;
		}
	}
}
