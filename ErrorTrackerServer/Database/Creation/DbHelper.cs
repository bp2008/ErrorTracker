using BPUtil;
using ErrorTrackerServer.Database.Creation;
using ErrorTrackerServer.Database.Global.Model;
using ErrorTrackerServer.Database.Project.v2;
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
		/// <summary>
		/// Use within a "using" block to guarantee correct disposal.  Provides SQL database access.  Not thread safe.
		/// </summary>
		public DbHelper()
		{
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
