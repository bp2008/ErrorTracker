using BPUtil;
using System;
using System.Runtime.Serialization;

namespace ErrorTrackerServer.Controllers
{
	[Serializable]
	public class FilterException : Exception
	{
		public BasicEventTimer timer;
		public FilterException(BasicEventTimer timer)
		{
			this.timer = timer;
		}

		public FilterException(BasicEventTimer timer, string message) : base(message)
		{
			this.timer = timer;
		}

		public FilterException(BasicEventTimer timer, string message, Exception innerException) : base(message, innerException)
		{
			this.timer = timer;
		}

		protected FilterException(SerializationInfo info, StreamingContext context) : base(info, context)
		{
			timer = new BasicEventTimer();
		}
	}
}