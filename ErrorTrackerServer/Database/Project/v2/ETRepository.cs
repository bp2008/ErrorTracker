using BPUtil;
using ErrorTrackerServer.Database.Creation;
using ErrorTrackerServer.Database.Project.Model;
using Npgsql;
using RepoDb;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ErrorTrackerServer.Database.Project.v2
{
	public class EtRepository : DbRepository<NpgsqlConnection>
	{
		public EtRepository() : base(DbCreation.GetConnectionString(), 60, RepoDb.Enumerations.ConnectionPersistency.Instance)
		{
		}
	}
}
