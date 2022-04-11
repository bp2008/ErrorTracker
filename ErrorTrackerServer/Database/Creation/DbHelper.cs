using BPUtil;
using ErrorTrackerServer.Database.Creation;
using ErrorTrackerServer.Database.Global.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace ErrorTrackerServer.Database.Creation
{
	public class DbHelper : DBBase
	{
		#region Constructor / Fields
		private object dbTransactionLock = new object();
		private string connectionString;
		/// <summary>
		/// Use within a "using" block to guarantee correct disposal.  Provides SQL database access.  Not thread safe.
		/// </summary>
		public DbHelper(string connectionString = null)
		{
			this.connectionString = connectionString;
		}
		#endregion
		#region Helpers
		protected override object GetTransactionLock()
		{
			return dbTransactionLock;
		}
		protected override string GetSchemaName()
		{
			throw new ApplicationException("This method is not supported by DbHelper.");
		}
		protected override string GetConnectionString()
		{
			if (connectionString == null)
				return DbCreation.GetConnectionString();
			return connectionString;
		}
		#endregion

		public T _ExecuteScalar<T>(string sql, object param = null)
		{
			return ExecuteScalar<T>(sql, param);
		}
		public int _ExecuteNonQuery(string sql, object param = null)
		{
			return ExecuteNonQuery(sql, param);
		}
	}
}
