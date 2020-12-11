using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ErrorTrackerServer
{
	public class PushSubscription
	{
		public string projectName;
		public int folderId;
		public string subscriptionKey;
		public PushSubscription()
		{
		}

		public PushSubscription(string projectName, int folderId, string subscriptionKey)
		{
			this.projectName = projectName;
			this.folderId = folderId;
			this.subscriptionKey = subscriptionKey;
		}
	}
}
