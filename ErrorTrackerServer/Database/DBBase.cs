using BPUtil;
using ErrorTrackerServer.Database.Creation;
using Npgsql;
using PetaPoco;
using PetaPoco.Providers;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Transactions;

namespace ErrorTrackerServer
{
	public abstract class DBBase : IDisposable
	{
		static DBBase()
		{
		}
		private object creationLock = new object();
		private IDatabase _db = null;
		private IDatabase db
		{
			get
			{
				if (_db == null)
				{
					lock (creationLock)
					{
						if (_db == null)
						{
							// PetaPoco db setup here:
							_db = DatabaseConfiguration.Build()
								.UsingConnectionString(GetConnectionString())
								.UsingProvider<PostgreSQLDatabaseProvider>()
								.UsingDefaultMapper<ConventionMapper>(m =>
								{
									m.InflectTableName = (inflector, s) => GetSchemaName() + "." + s.ToLower();
									m.InflectColumnName = (inflector, s) => s.ToLower();
									// Teach PetaPoco how to handle uint properties by casting to and from int.
									m.FromDbConverter = (targetProperty, sourceType) =>
									{
										if (targetProperty != null && targetProperty.PropertyType == typeof(uint) && sourceType == typeof(int))
											return i => (uint)(int)i;
										return null;
									};
									m.ToDbConverter = sourceProperty =>
									{
										if (sourceProperty != null && sourceProperty.PropertyType == typeof(uint))
											return i => (int)(uint)i;
										return null;
									};
								})
								.Create();
						}
					}
				}
				return _db;
			}
		}
		private static int UIntToInt(uint val)
		{
			return (int)val;
		}
		private static uint IntToUInt(int val)
		{
			return (uint)val;
		}
		/// <summary>
		/// When overridden in a derived class, this method returns an object for which the lock should be held before beginning any DB transactions.
		/// </summary>
		/// <returns></returns>
		protected abstract object GetTransactionLock();
		/// <summary>
		/// When overridden in a derived class, this method returns the name of the db schema that is to be used when making certain database requests.
		/// </summary>
		/// <returns></returns>
		protected abstract string GetSchemaName();
		/// <summary>
		/// When overridden in a derived class, this method returns the connection string to be used for database requests.
		/// </summary>
		/// <returns></returns>
		protected abstract string GetConnectionString();
		#region Helpers

		/// <summary>
		/// <para>Ensures that a transaction is open, then calls the method.  The transaction is automatically committed when the method completes.  If an exception is thrown, the transaction is automatically rolled back and the exception is rethrown.</para>
		/// </summary>
		/// <param name="action">Action method to call. The method must use the transaction for all database queries.</param>
		/// <returns></returns>
		public void RunInTransaction(Action action)
		{
			using (ITransaction transaction = db.GetTransaction())
			{
				action();
				transaction.Complete();
			}
		}

		/// <summary>
		/// <para>Ensures that a transaction is open, then calls the method.  The transaction is automatically committed when the method completes.  If an exception is thrown, the transaction is automatically rolled back and the exception is rethrown.</para>
		/// </summary>
		/// <param name="func">Func method to call. The method must use the transaction for all database queries.</param>
		/// <returns></returns>
		public T RunInTransaction<T>(Func<T> func)
		{
			T retVal = default(T);
			RunInTransaction(() =>
			{
				retVal = func();
			});
			return retVal;
		}

		/// <summary>
		/// Inserts the specified object into the correct table for this project.
		/// </summary>
		/// <typeparam name="T">Object type</typeparam>
		/// <param name="obj">Object to insert.</param>
		protected object Insert<T>(T obj)
		{
			return db.Insert(obj);
		}
		/// <summary>
		/// Inserts the specified objects into the correct table for this project.
		/// </summary>
		/// <typeparam name="T">Object type</typeparam>
		/// <param name="objs">Objects to insert.</param>
		protected int InsertAll<T>(IEnumerable<T> objs)
		{
			foreach (T obj in objs)
				db.Insert(obj);
			return objs.Count();
		}
		/// <summary>
		/// Gets objects with the specified key.
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="key"></param>
		/// <param name="transaction"></param>
		/// <returns></returns>
		protected List<T> Query<T>(string keyColumnName, object key)
		{
			return db.Fetch<T>("WHERE " + keyColumnName + " = @0", key);
		}
		protected List<T> QueryAll<T>()
		{
			return db.Fetch<T>();
		}
		protected int Delete<T>(object key)
		{
			return db.Delete(key);
		}
		protected int Update<T>(T obj)
		{
			return db.Update(obj);
		}
		protected int UpdateAll<T>(IEnumerable<T> objs)
		{
			foreach (T obj in objs)
				return db.Update(obj);
			return objs.Count();
		}
		/// <summary>
		/// Executes a SQL statement using ExecuteNonQuery.
		/// </summary>
		/// <param name="sql">The command text to be used.</param>
		/// <param name="param">The parameters/values defined in the System.Data.IDbCommand.CommandText property. Supports a dynamic object, System.Collections.Generic.IDictionary`2, System.Dynamic.ExpandoObject, RepoDb.QueryField, RepoDb.QueryGroup and an enumerable of RepoDb.QueryField objects.</param>
		/// <returns></returns>
		protected int ExecuteNonQuery(string sql, object param = null)
		{
			return db.Execute(sql, param);
		}
		/// <summary>
		/// Executes a SQL statement using ExecuteQuery.
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="sql">The command text to be used.</param>
		/// <param name="param">The parameters/values defined in the System.Data.IDbCommand.CommandText property. Supports a dynamic object, System.Collections.Generic.IDictionary`2, System.Dynamic.ExpandoObject, RepoDb.QueryField, RepoDb.QueryGroup and an enumerable of RepoDb.QueryField objects.</param>
		/// <returns></returns>
		protected IEnumerable<T> ExecuteQuery<T>(string sql, object param = null)
		{
			return db.Query<T>(sql, param);
		}
		/// <summary>
		/// Executes a SQL statement using ExecuteScalar. Returns the value in the first column of the first row.
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="sql">The command text to be used.</param>
		/// <param name="param">The parameters/values defined in the System.Data.IDbCommand.CommandText property. Supports a dynamic object, System.Collections.Generic.IDictionary`2, System.Dynamic.ExpandoObject, RepoDb.QueryField, RepoDb.QueryGroup and an enumerable of RepoDb.QueryField objects.</param>
		/// <returns></returns>
		protected T ExecuteScalar<T>(string sql, object param = null)
		{
			return db.ExecuteScalar<T>(sql, param);
		}
		/// <summary>
		/// Executes a stored procedure.
		/// </summary>
		/// <typeparam name="T">Type of object expected as a return value.</typeparam>
		/// <param name="storedProcedureName">Stored Procedure Name</param>
		/// <param name="args">Arguments to the stored procedure</param>
		/// <returns></returns>
		public IEnumerable<T> SP<T>(string storedProcedureName, object args)
		{
			return db.FetchProc<T>(storedProcedureName, args);
		}
		protected IEnumerable<T> DeferredSP<T>(string storedProcedureName, params object[] args)
		{
			return db.QueryProc<T>(storedProcedureName, args);
		}
		#endregion

		public void Dispose()
		{
			db?.Dispose();
		}
	}
}
