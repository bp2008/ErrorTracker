using RepoDb.Attributes;

namespace ErrorTrackerServer.Database.Project.Model
{
	public class DbVersion
	{
		[Map("currentversion")]
		public int CurrentVersion { get; set; }
	}
}
