using BPUtil;
using ErrorTrackerServer.Database.Project.v2;
using RepoDb;
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

namespace ErrorTrackerServer
{
	public abstract class DBBase : IDisposable
	{
		static DBBase()
		{
		}
		private EtRepository repo = new EtRepository();
		public IDbTransaction CurrentTransaction { get; private set; } = null;
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
		#region Helpers

		/// <summary>
		/// <para>Ensures that a transaction is open, then calls the method.  The transaction is automatically committed when the method completes.  If an exception is thrown, the transaction is automatically rolled back and the exception is rethrown.</para>
		/// </summary>
		/// <param name="action">Action method to call. The method must use the transaction for all database queries.</param>
		/// <returns></returns>
		public void RunInTransaction(Action action)
		{
			IDbTransaction existingTransaction = CurrentTransaction;
			if (existingTransaction != null)
				action();
			else
				using (IDbTransaction transaction = repo.CreateConnection().EnsureOpen().BeginTransaction())
				{
					CurrentTransaction = transaction;
					try
					{
						action();
						transaction.Commit();
					}
					catch (Exception ex)
					{
						try
						{
							transaction.Rollback();
							throw;
						}
						catch (Exception ex2)
						{
							throw new AggregateException(ex, ex2);
						}
					}
					finally
					{
						CurrentTransaction = null;
					}
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
			return repo.Insert(GetSchemaName() + "." + typeof(T).Name, obj, transaction: CurrentTransaction);
		}
		/// <summary>
		/// Inserts the specified objects into the correct table for this project.
		/// </summary>
		/// <typeparam name="T">Object type</typeparam>
		/// <param name="objs">Objects to insert.</param>
		protected int InsertAll<T>(IEnumerable<T> objs) where T : class
		{
			return repo.InsertAll(GetSchemaName() + "." + typeof(T).Name, objs, transaction: CurrentTransaction);
		}
		/// <summary>
		/// Gets objects with the specified key.
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="key"></param>
		/// <param name="transaction"></param>
		/// <returns></returns>
		protected List<T> Query<T>(object key) where T : class
		{
			return repo.Query<T>(GetSchemaName() + "." + typeof(T).Name, key, transaction: CurrentTransaction).ToList();
		}
		protected List<T> QueryAll<T>() where T : class
		{
			return repo.QueryAll<T>(GetSchemaName() + "." + typeof(T).Name, transaction: CurrentTransaction).ToList();
		}
		protected int Delete<T>(object key) where T : class
		{
			return repo.Delete(GetSchemaName() + "." + typeof(T).Name, transaction: CurrentTransaction);
		}
		protected int Update<T>(T obj) where T : class
		{
			return repo.Update<T>(GetSchemaName() + "." + typeof(T).Name, obj, transaction: CurrentTransaction);
		}
		protected int UpdateAll<T>(IEnumerable<T> objs) where T : class
		{
			return repo.UpdateAll<T>(GetSchemaName() + "." + typeof(T).Name, objs, transaction: CurrentTransaction);
		}
		/// <summary>
		/// Executes a SQL statement using ExecuteNonQuery.
		/// </summary>
		/// <param name="sql">The command text to be used.</param>
		/// <param name="param">The parameters/values defined in the System.Data.IDbCommand.CommandText property. Supports a dynamic object, System.Collections.Generic.IDictionary`2, System.Dynamic.ExpandoObject, RepoDb.QueryField, RepoDb.QueryGroup and an enumerable of RepoDb.QueryField objects.</param>
		/// <returns></returns>
		protected int ExecuteNonQuery(string sql, object param = null)
		{
			return repo.ExecuteNonQuery(sql, param, transaction: CurrentTransaction);
		}
		/// <summary>
		/// Executes a SQL statement using ExecuteQuery.
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="sql">The command text to be used.</param>
		/// <param name="param">The parameters/values defined in the System.Data.IDbCommand.CommandText property. Supports a dynamic object, System.Collections.Generic.IDictionary`2, System.Dynamic.ExpandoObject, RepoDb.QueryField, RepoDb.QueryGroup and an enumerable of RepoDb.QueryField objects.</param>
		/// <returns></returns>
		protected IEnumerable<dynamic> ExecuteQuery(string sql, object param = null)
		{
			return repo.ExecuteQuery(sql, param, transaction: CurrentTransaction);
		}
		/// <summary>
		/// Executes a SQL statement using ExecuteQuery.
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="sql">The command text to be used.</param>
		/// <param name="param">The parameters/values defined in the System.Data.IDbCommand.CommandText property. Supports a dynamic object, System.Collections.Generic.IDictionary`2, System.Dynamic.ExpandoObject, RepoDb.QueryField, RepoDb.QueryGroup and an enumerable of RepoDb.QueryField objects.</param>
		/// <returns></returns>
		protected IEnumerable<T> ExecuteQuery<T>(string sql, object param = null) where T : class
		{
			return repo.ExecuteQuery<T>(sql, param, transaction: CurrentTransaction);
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
			return repo.ExecuteScalar<T>(sql, param, transaction: CurrentTransaction);
		}
		/// <summary>
		/// Executes a stored procedure.
		/// </summary>
		/// <typeparam name="T">Type of object expected as a return value.</typeparam>
		/// <param name="storedProcedureName">Stored Procedure Name</param>
		/// <param name="args">Arguments to the stored procedure</param>
		/// <returns></returns>
		public IEnumerable<T> SP<T>(string storedProcedureName, object args) where T : class
		{
			return repo.ExecuteQuery<T>(storedProcedureName, args, CommandType.StoredProcedure, transaction: CurrentTransaction);
		}
		#endregion

		public void Dispose()
		{
			IDbTransaction t = CurrentTransaction;
			if (t != null)
			{
				Logger.Debug(this.GetType().Name + ".Dispose() found an existing transaction which will now be rolled back.");
				try
				{
					t.Rollback();
				}
				catch (Exception ex)
				{
					Logger.Debug(ex);
				}
				CurrentTransaction = null;
			}
			repo.Dispose();
		}
	}
}
