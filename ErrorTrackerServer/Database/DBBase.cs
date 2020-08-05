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
		/// Lazy-loaded database connection.
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
		protected abstract void LockedTransaction(Action action);
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
		/// Robustly executes the specified query.
		/// </summary>
		/// <param name="sql">The fully escaped SQL.</param>
		/// <param name="args">Arguments to substitute for the occurences of '?' in the query.</param>
		/// <returns></returns>
		protected int Execute(string sql, params object[] args)
		{
			return Robustify(() =>
			{
				return conn.Value.Execute(sql, args);
			});
		}

		protected T ExecuteScalar<T>(string sql, params object[] args)
		{
			return Robustify(() =>
			{
				return conn.Value.ExecuteScalar<T>(sql, args);
			});
		}
		/// <summary>
		/// Robustly executes the specified query.
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
		/// Robustly executes the specified query.
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

		protected int InsertAll<T>(IEnumerable<T> objects)
		{
			return Robustify(() =>
			{
				return conn.Value.InsertAll(objects, false);
			});
		}

		protected int Insert<T>(T obj)
		{
			return Robustify(() =>
			{
				return conn.Value.Insert(obj);
			});
		}

		protected int Delete(object objectToDelete)
		{
			return Robustify(() =>
			{
				return conn.Value.Delete(objectToDelete);
			});
		}
		protected int Delete<T>(object primaryKey)
		{
			return Robustify(() =>
			{
				return conn.Value.Delete<T>(primaryKey);
			});
		}

		protected int Update(object obj)
		{
			return Robustify(() =>
			{
				return conn.Value.Update(obj);
			});
		}

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
