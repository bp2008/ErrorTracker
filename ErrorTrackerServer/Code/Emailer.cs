using BPUtil;
using BPUtil.MVC;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Mail;
using System.Net.NetworkInformation;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ErrorTrackerServer
{
	public static class Emailer
	{
		private static SimpleThreadPool pool = new SimpleThreadPool("EmailSendingPool", 0, 4, 10000, true, Logger.Debug);
		static Emailer()
		{
			CertificateValidation.RegisterCallback(EmailerCertCallback);
		}
		private static bool EmailerCertCallback(object sender, X509Certificate certificate, X509Chain chain, SslPolicyErrors sslPolicyErrors)
		{
			return sender is SmtpClient;
		}
		/// <summary>
		/// True if the email configuration is sufficient for us to try sending email.
		/// </summary>
		public static bool Enabled
		{
			get
			{
				return !string.IsNullOrWhiteSpace(Settings.data.smtpHost)
					&& !string.IsNullOrWhiteSpace(Settings.data.smtpSendFrom);
			}
		}
		/// <summary>
		/// Sends an email.
		/// </summary>
		/// <param name="to">Email addresses, comma or semicolon separated, to send to (using the "Bcc" field). Validation errors are silently ignored and may result in no email being sent.</param>
		/// <param name="subject">Subject line</param>
		/// <param name="body">Body</param>
		/// <param name="bodyHtml">If true, the body is an HTML body.</param>
		public static void SendEmail(string to, string subject, string body, bool bodyHtml)
		{
			if (!Enabled)
				return;

			MailMessage message = new MailMessage();
			message.From = new MailAddress(Settings.data.smtpSendFrom);

			string[] allToAddresses = to.Split(";,".ToCharArray(), StringSplitOptions.RemoveEmptyEntries);
			foreach (string toAddress in allToAddresses)
			{
				try
				{
					message.Bcc.Add(toAddress.Trim());
				}
				catch (FormatException) { }
			}
			if (message.Bcc.Count == 0)
				return;

			message.Subject = subject;
			message.Body = body;
			message.IsBodyHtml = bodyHtml;

			pool.Enqueue(() =>
			{
				using (SmtpClient client = new SmtpClient())
				{
					client.Host = Settings.data.smtpHost;
					client.Port = Settings.data.smtpPort;
					client.EnableSsl = Settings.data.smtpSsl;
					if (!string.IsNullOrEmpty(Settings.data.smtpUser) && !string.IsNullOrEmpty(Settings.data.smtpPass))
						client.Credentials = new NetworkCredential(Settings.data.smtpUser, Settings.data.smtpPass);
					try
					{
						client.Send(message);
					}
					catch (Exception ex1)
					{
						Logger.Debug(ex1, "Failed to send email (attempt 1/2)");
						Thread.Sleep(1000);
						try
						{
							client.Send(message);
						}
						catch (Exception ex2)
						{
							Logger.Debug(ex2, "Failed to send email (attempt 2/2)");
						}
					}
				}
			});
		}
		/// <summary>
		/// Sends an error report by email to the configured recipient.
		/// </summary>
		/// <param name="body">Body</param>
		/// <param name="bodyHtml">If true, the body is an HTML body.</param>
		public static void SendError(string body, bool bodyHtml)
		{
			if (!string.IsNullOrWhiteSpace(Settings.data.defaultErrorEmail))
				SendEmail(Settings.data.defaultErrorEmail, "Error in " + Settings.data.systemName, body, bodyHtml);
		}
		/// <summary>
		/// Sends an error report by email to the configured recipient.
		/// </summary>
		/// <param name="context">MVC request context.</param>
		/// <param name="heading">Text to include at the very top of the email.</param>
		/// <param name="ex">Exception</param>
		public static void SendError(RequestContext context, string heading, Exception ex)
		{
			SendError(context, heading, ex.ToString());
		}
		/// <summary>
		/// Sends an error report by email to the configured recipient.
		/// </summary>
		/// <param name="context">MVC request context.</param>
		/// <param name="heading">Text to include at the very top of the email.</param>
		/// <param name="errorMessageText">Error message text to include after the context area.</param>
		public static void SendError(RequestContext context, string heading, string errorMessageText)
		{
			if (!string.IsNullOrWhiteSpace(heading))
				heading += "\r\n\r\n";
			if (heading == null)
				heading = "";
			string ctx = "";
			if (context != null)
			{
				ctx = "Client IP: " + context.httpProcessor.RemoteIPAddressStr + "\r\n"
					  + "URL: " + context.OriginalRequestPath + "\r\n";
			}
			SendError(heading
					+ "Date: " + DateTime.Now.ToString() + "\r\n"
					+ ctx
					+ "\r\n"
					+ errorMessageText,
					false);
		}
	}
}
