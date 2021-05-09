using BPUtil;
using SQLite;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ErrorTrackerServer
{
	public abstract class DBBase : IDisposable
	{
		/// <summary>
		/// Lazy-loaded database connection. This should mostly not be accessed directly because it isn't very robust in a high-concurrency environment.
		/// </summary>
		protected Lazy<SQLiteConnection> conn;
		/// <summary>
		/// True if the database is currently in a transaction.
		/// </summary>
		public bool IsInTransaction
		{
			get
			{
				return conn.IsValueCreated ? conn.Value.IsInTransaction : false;
			}
		}
		#region Helpers
		/// <summary>
		/// Runs the specified Action in a transaction that will automatically retry if the database is locked.
		/// </summary>
		/// <param name="action"></param>
		public void LockedTransaction(Action action)
		{
			object transactionLock = GetTransactionLock();
			lock (transactionLock)
			{
				Robustify(() =>
				{
					conn.Value.RunInTransaction(action);
				});
			}
		}
		protected abstract object GetTransactionLock();
		/// <summary>
		/// Runs the specified action.  While the database is locked (deadlock detected?), repeats the call for up to this many milliseconds.
		/// Robustify calls within a transaction action have no effect. LockedTransaction calls have their own retry logic in case of database lock.
		/// </summary>
		/// <param name="action"></param>
		/// <param name="runtimeLimitMs"></param>
		protected void Robustify(Action action, int runtimeLimitMs = 60000)
		{
			if (IsInTransaction)
			{
				action();
				return;
			}
			Stopwatch sw = Stopwatch.StartNew();
			while (true)
			{
				try
				{
					conn.Value.RunInTransaction(action);
					return;
				}
				catch (SQLiteException ex)
				{
					if (ex.Message == "database is locked" && sw.ElapsedMilliseconds < runtimeLimitMs)
						Thread.Sleep(StaticRandom.Next(1, 11));
					else
						throw ex;
				}
			}
		}
		/// <summary>
		/// Runs the specified func and returns its return value.  While the database is locked (deadlock detected?), repeats the call for up to this many milliseconds.
		/// Robustify calls within a transaction action have no effect. LockedTransaction calls have their own retry logic in case of database lock.
		/// </summary>
		/// <param name="func"></param>
		/// <param name="runtimeLimitMs"></param>
		protected T Robustify<T>(Func<T> func, int runtimeLimitMs = 60000)
		{
			if (IsInTransaction)
				return func();
			Stopwatch sw = Stopwatch.StartNew();
			while (true)
			{
				try
				{
					T retVal = default;
					conn.Value.RunInTransaction(() =>
					{
						retVal = func();
					});
					return retVal;
				}
				catch (SQLiteException ex)
				{
					if (ex.Message == "database is locked" && sw.ElapsedMilliseconds < runtimeLimitMs)
						Thread.Sleep(StaticRandom.Next(1, 11));
					else
						throw ex;
				}
			}
		}
		/// <summary>
		/// (Robustified). Creates a SQLiteCommand given the command text (SQL) with arguments. Place a '?' in the command text for each of the arguments and then executes that command. Use this method instead of Query when you don't expect rows back. Such cases include INSERTs, UPDATEs, and DELETEs. You can set the Trace or TimeExecution properties of the connection to profile execution.
		/// </summary>
		/// <param name="sql">The fully escaped SQL.</param>
		/// <param name="args">Arguments to substitute for the occurences of '?' in the query.</param>
		/// <returns>The number of rows modified in the database as a result of this execution.</returns>
		protected int Execute(string sql, params object[] args)
		{
			return Robustify(() =>
			{
				return conn.Value.Execute(sql, args);
			});
		}
		/// <summary>
		/// (Robustified). Creates a SQLiteCommand given the command text (SQL) with arguments. Place a '?' in the command text for each of the arguments and then executes that command. Use this method when return primitive values. You can set the Trace or TimeExecution properties of the connection to profile execution.
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="sql">The fully escaped SQL.</param>
		/// <param name="args">Arguments to substitute for the occurences of '?' in the query.</param>
		/// <returns>The number of rows modified in the database as a result of this execution.</returns>
		protected T ExecuteScalar<T>(string sql, params object[] args)
		{
			return Robustify(() =>
			{
				return conn.Value.ExecuteScalar<T>(sql, args);
			});
		}
		/// <summary>
		/// (Robustified). Creates a SQLiteCommand given the command text (SQL) with arguments. Place a '?' in the command text for each of the arguments and then executes that command. It returns each row of the result using the mapping automatically generated for the given type.
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="query">The fully escaped SQL.</param>
		/// <param name="args">Arguments to substitute for the occurences of '?' in the query.</param>
		/// <returns>An enumerable with one result for each row returned by the query.</returns>
		protected List<T> Query<T>(string query, params object[] args) where T : new()
		{
			return Robustify(() =>
			{
				return conn.Value.Query<T>(query, args);
			});
		}
		/// <summary>
		/// (Robustified). Creates a SQLiteCommand given the command text (SQL) with arguments. Place a '?' in the command text for each of the arguments and then executes that command. It returns each row of the result using the mapping automatically generated for the given type.
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="query">The fully escaped SQL.</param>
		/// <param name="args">Arguments to substitute for the occurences of '?' in the query.</param>
		/// <returns>An enumerable with one result for each row returned by the query.</returns>
		protected IEnumerable<T> DeferredQuery<T>(string query, params object[] args) where T : new()
		{
			return Robustify(() =>
			{
				return conn.Value.DeferredQuery<T>(query, args);
			});
		}
		/// <summary>
		/// (Robustified). Inserts all specified objects.
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="objects">An System.Collections.IEnumerable of the objects to insert. A boolean indicating if the inserts should be wrapped in a transaction.</param>
		/// <returns>The number of rows added to the table.</returns>
		protected int InsertAll(System.Collections.IEnumerable objects)
		{
			return Robustify(() =>
			{
				return conn.Value.InsertAll(objects, false);
			});
		}
		/// <summary>
		/// (Robustified). Inserts the given object (and updates its auto incremented primary key if it has one). The return value is the number of rows added to the table.
		/// </summary>
		/// <param name="obj">The object to insert.</param>
		/// <returns>The number of rows added to the table.</returns>
		protected int Insert(object obj)
		{
			return Robustify(() =>
			{
				return conn.Value.Insert(obj);
			});
		}
		/// <summary>
		/// (Robustified). Deletes the given object from the database using its primary key.
		/// </summary>
		/// <param name="objectToDelete">The object to delete. It must have a primary key designated using the PrimaryKeyAttribute.</param>
		/// <returns>The number of rows deleted.</returns>
		protected int Delete(object objectToDelete)
		{
			return Robustify(() =>
			{
				return conn.Value.Delete(objectToDelete);
			});
		}
		/// <summary>
		/// (Robustified). Deletes the object with the specified primary key.
		/// </summary>
		/// <typeparam name="T">The type of object.</typeparam>
		/// <param name="primaryKey">The primary key of the object to delete.</param>
		/// <returns>The number of objects deleted.</returns>
		protected int Delete<T>(object primaryKey)
		{
			return Robustify(() =>
			{
				return conn.Value.Delete<T>(primaryKey);
			});
		}
		/// <summary>
		/// (Robustified). Updates all of the columns of a table using the specified object except for its primary key. The object is required to have a primary key.
		/// </summary>
		/// <param name="obj">The object to update. It must have a primary key designated using the PrimaryKeyAttribute.</param>
		/// <returns>The number of rows updated.</returns>
		protected int Update(object obj)
		{
			return Robustify(() =>
			{
				return conn.Value.Update(obj);
			});
		}
		/// <summary>
		/// (Robustified). Updates all specified objects.
		/// </summary>
		/// <param name="objects">An System.Collections.IEnumerable of the objects to insert.</param>
		/// <returns>The number of rows modified.</returns>
		protected int UpdateAll(System.Collections.IEnumerable objects)
		{
			return Robustify(() =>
			{
				return conn.Value.UpdateAll(objects, false);
			});
		}
		#endregion

		#region IDisposable
		private bool disposedValue;
		protected virtual void Dispose(bool disposing)
		{
			if (!disposedValue)
			{
				if (disposing)
				{
					// dispose managed state (managed objects)
					if (conn.IsValueCreated)
						conn.Value.Dispose();
				}

				// free unmanaged resources (unmanaged objects) and override finalizer
				// set large fields to null
				disposedValue = true;
			}
		}

		// override finalizer only if 'Dispose(bool disposing)' has code to free unmanaged resources
		// ~DB()
		// {
		//     // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
		//     Dispose(disposing: false);
		// }

		public void Dispose()
		{
			// Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
			Dispose(disposing: true);
			GC.SuppressFinalize(this);
		}
		#endregion
	}
}
