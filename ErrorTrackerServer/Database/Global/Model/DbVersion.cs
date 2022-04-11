using RepoDb.Attributes;

namespace ErrorTrackerServer.Database.Global.Model
{
	[Map("ErrorTrackerGlobal.DbVersion")]
	public class DbVersion
	{
		public int CurrentVersion { get; set; }
	}
}
