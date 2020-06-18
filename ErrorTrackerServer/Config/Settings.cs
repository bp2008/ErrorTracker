using BPUtil;
using BPUtil.SimpleHttp;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ErrorTrackerServer
{
	public class Settings : SerializableObjectBase
	{
		/// <summary>
		/// The settings object for ErrorTrackerServer.  Call Save() on this instance to commit changes to disk.
		/// </summary>
		public static Settings data = new Settings();

		/// <summary>
		/// Controls where web server files are loaded from. If true, loaded from project directory. If false, loaded from executable's directory.
		/// </summary>
		public bool devMode = false;
		/// <summary>
		/// Virtual directory path, if one is required for incoming requests to an upstream proxy server.
		/// </summary>
		public string appPath = "/";
		/// <summary>
		/// HTTP port.
		/// </summary>
		public int port_http = 80;
		/// <summary>
		/// HTTPS port.
		/// </summary>
		public int port_https = 443;
		/// <summary>
		/// Path to certificate file (*.pfx)
		/// </summary>
		public string certificatePath;
		/// <summary>
		/// Optional password for .pfx certificate file.
		/// </summary>
		public string certificatePassword;
		/// <summary>
		/// A string indicating an additional css class to apply to the login root element.
		/// </summary>
		public string loginStyle = "wallpaper";

		public string GetWWWDirectoryBase()
		{
			if (devMode)
				return Globals.ApplicationDirectoryBase + "../../www/";
			else
				return Globals.ApplicationDirectoryBase + "www/";
		}
		/// <summary>
		/// Returns the app path beginning and ending with '/'.  Default App Path: "/"
		/// </summary>
		/// <returns></returns>
		public string GetAppPath()
		{
			if (!string.IsNullOrWhiteSpace(appPath))
			{
				string ap = '/' + appPath.Trim().Trim('/', ' ', '\r', '\n', '\t') + '/';
				if (ap != "//")
					return ap;
			}
			return "/";
		}

		/// <summary>
		/// Removes the configured appPath from the start of the incoming request URL, if it is found there.
		/// </summary>
		/// <param name="p">HttpProcessor to remove the appPath from.</param>
		public void RemoveAppPath(HttpProcessor p)
		{
			string ap = appPath == null ? "/" : ('/' + appPath.Trim('/', ' ', '\r', '\n', '\t'));
			if (ap != "" && p.request_url.AbsolutePath.StartsWith(ap, StringComparison.OrdinalIgnoreCase))
			{
				string absolutePath = p.request_url.AbsolutePath.Substring(ap.Length);
				if (absolutePath.StartsWith("/"))
					absolutePath = absolutePath.Substring(1);
				p.request_url = new Uri(p.request_url.Scheme + "://" + p.request_url.DnsSafeHost + (p.request_url.IsDefaultPort ? "" : ":" + p.request_url.Port) + "/" + absolutePath + p.request_url.Query);
				p.requestedPage = absolutePath;
			}
		}
		#region Project Management
		/// <summary>
		/// List of projects. For speed, each project has its own database file.
		/// </summary>
		public List<Project> Internal_Projects = new List<Project>();
		private ReaderWriterLockSlim projectsLock = new ReaderWriterLockSlim(LockRecursionPolicy.SupportsRecursion);
		/// <summary>
		/// Gets the project with the specified name. Returns null if the project is not found.
		/// </summary>
		/// <param name="projectName">Project name (case-insensitive).</param>
		/// <returns></returns>
		public Project GetProject(string projectName)
		{
			return _GetListItem(projectName, Internal_Projects, p => p.Name, projectsLock);
		}
		/// <summary>
		/// Adds the project.  Returns true if successful or false if a project with the same name already exists.  Project names are case-insensitive.
		/// </summary>
		/// <param name="project">Project to add.</param>
		/// <returns></returns>
		public bool TryAddProject(Project project)
		{
			return _TryAddListItem(project, Internal_Projects, p => p.Name, projectsLock);
		}
		/// <summary>
		/// Removes the project with the specified name, returning the project that was removed.  Returns null if the project could not be found.
		/// </summary>
		/// <param name="projectName">Project name (case-insensitive).</param>
		/// <returns></returns>
		public Project TryRemoveProject(string projectName)
		{
			Project project = _TryRemoveListItem(projectName, Internal_Projects, p => p.Name, projectsLock);
			if (project != null)
			{
				foreach (User user in GetAllUsers())
					user.DisallowProject(projectName);
				return project;
			}
			return project;
		}
		/// <summary>
		/// Gets the number of projects.
		/// </summary>
		/// <returns></returns>
		public int CountProjects()
		{
			return _CountListItems(Internal_Projects, projectsLock);
		}
		/// <summary>
		/// Gets a snapshot of the Project list.
		/// </summary>
		public List<Project> GetAllProjects()
		{
			return _GetList(Internal_Projects, projectsLock);
		}
		#endregion
		#region User Management
		/// <summary>
		/// Don't query this directly -- use instance methods like <see cref="GetUser"/> instead to guarantee thread safety.
		/// (List of users)
		/// </summary>
		public List<User> Internal_Users = new List<User>();
		private ReaderWriterLockSlim usersLock = new ReaderWriterLockSlim(LockRecursionPolicy.SupportsRecursion);

		/// <summary>
		/// Gets the user with the specified name. Returns null if the user is not found.
		/// </summary>
		/// <param name="userName">User name (case-insensitive).</param>
		/// <returns></returns>
		public User GetUser(string userName)
		{
			return _GetListItem(userName, Internal_Users, u => u.Name, usersLock);
		}

		/// <summary>
		/// Adds the user.  Returns true if successful or false if a user with the same name already exists.  User names are case-insensitive.
		/// </summary>
		/// <param name="user">User to add.</param>
		/// <returns></returns>
		public bool TryAddUser(User user)
		{
			return _TryAddListItem(user, Internal_Users, u => u.Name, usersLock);
		}

		/// <summary>
		/// Removes the user with the specified name, returning the user that was removed.  Returns null if the user could not be found.
		/// </summary>
		/// <param name="userName">User name (case-insensitive).</param>
		/// <returns></returns>
		public User TryRemoveUser(string userName)
		{
			return _TryRemoveListItem(userName, Internal_Users, u => u.Name, usersLock);
		}
		/// <summary>
		/// Gets the number of users.
		/// </summary>
		/// <returns></returns>
		public int CountUsers()
		{
			return _CountListItems(Internal_Users, usersLock);
		}

		/// <summary>
		/// Gets a snapshot of the User list.
		/// </summary>
		public List<User> GetAllUsers()
		{
			return _GetList(Internal_Users, usersLock);
		}
		#endregion
		#region Generic thread-safe list item get/add/remove
		/// <summary>
		/// Gets the item with the specified name. Returns null if the item is not found.
		/// </summary>
		/// <typeparam name="T">Type of item.</typeparam>
		/// <param name="itemName">Item name (case-insensitive)</param>
		/// <param name="items">Item List.</param>
		/// <param name="GetNameFn">Function accepting an item and returning its name.</param>
		/// <param name="rwLock">A lock to control access to the item list.</param>
		/// <returns></returns>
		internal static T _GetListItem<T>(string itemName, List<T> items, Func<T, string> GetNameFn, ReaderWriterLockSlim rwLock)
		{
			if (itemName != null)
			{
				rwLock.EnterReadLock();
				try
				{
					foreach (T item in items)
						if (itemName.Equals(GetNameFn(item), StringComparison.OrdinalIgnoreCase))
							return item;
				}
				finally
				{
					rwLock.ExitReadLock();
				}
			}
			return default(T);
		}
		/// <summary>
		/// Adds the item.  Returns true if successful or false if an item with the same name already exists.  Item names are case-insensitive.
		/// </summary>
		/// <typeparam name="T">Type of item.</typeparam>
		/// <param name="item">Item to add.</param>
		/// <param name="items">Item List.</param>
		/// <param name="GetNameFn">Function accepting an item and returning its name.</param>
		/// <param name="rwLock">A lock to control access to the item list.</param>
		/// <returns></returns>
		internal static bool _TryAddListItem<T>(T item, List<T> items, Func<T, string> GetNameFn, ReaderWriterLockSlim rwLock)
		{
			if (item != null)
			{
				string name = GetNameFn(item);
				if (name != null)
				{
					rwLock.EnterUpgradeableReadLock();
					try
					{
						if (_GetListItem(name, items, GetNameFn, rwLock) != null)
							return false;
						rwLock.EnterWriteLock();
						try
						{
							items.Add(item);
							items.Sort((a, b) =>
							{
								return GetNameFn(a).CompareTo(GetNameFn(b));
							});
						}
						finally
						{
							rwLock.ExitWriteLock();
						}
						return true;
					}
					finally
					{
						rwLock.ExitUpgradeableReadLock();
					}
				}
			}
			return false;
		}

		/// <summary>
		/// Removes the item with the specified name, returning the item that was removed.  Returns null if the item could not be found.
		/// </summary>
		/// <typeparam name="T">Type of item.</typeparam>
		/// <param name="userName">Item name (case-insensitive).</param>
		/// <param name="items">Item List.</param>
		/// <param name="GetNameFn">Function accepting an item and returning its name.</param>
		/// <param name="rwLock">A lock to control access to the item list.</param>
		/// <returns></returns>
		internal static T _TryRemoveListItem<T>(string itemName, List<T> items, Func<T, string> GetNameFn, ReaderWriterLockSlim rwLock)
		{
			if (itemName != null)
			{
				rwLock.EnterUpgradeableReadLock();
				try
				{
					for (int i = 0; i < items.Count; i++)
					{
						T item = items[i];
						if (GetNameFn(item).Equals(itemName, StringComparison.OrdinalIgnoreCase))
						{
							rwLock.EnterWriteLock();
							try
							{
								items.RemoveAt(i);
							}
							finally
							{
								rwLock.ExitWriteLock();
							}
							return item;
						}
					}
				}
				finally
				{
					rwLock.ExitUpgradeableReadLock();
				}
			}
			return default(T);
		}

		/// <summary>
		/// Gets the number of items in the list.
		/// </summary>
		/// <param name="items">Item List.</param>
		/// <param name="rwLock">A lock to control access to the item list.</param>
		/// <returns></returns>
		internal static int _CountListItems<T>(List<T> items, ReaderWriterLockSlim rwLock)
		{
			rwLock.EnterReadLock();
			try
			{
				return items.Count;
			}
			finally
			{
				rwLock.ExitReadLock();
			}
		}

		/// <summary>
		/// Clears [oldItems] and adds to it all the items from [newItems].
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="newItems">List containing new items.</param>
		/// <param name="oldItems">List to clear and make into a mirror of newItems.</param>
		/// <param name="rwLock">A lock to control access to the item list.</param>
		internal static void _UpdateList<T>(List<T> newItems, List<T> oldItems, ReaderWriterLockSlim rwLock)
		{
			rwLock.EnterWriteLock();
			try
			{
				oldItems.Clear();
				oldItems.AddRange(newItems);
			}
			finally
			{
				rwLock.ExitWriteLock();
			}
		}

		/// <summary>
		/// Gets a snapshot of the list.
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="items">Item List.</param>
		/// <param name="rwLock">A lock to control access to the item list.</param>
		internal static List<T> _GetList<T>(List<T> items, ReaderWriterLockSlim rwLock)
		{
			rwLock.EnterReadLock();
			try
			{
				return new List<T>(items);
			}
			finally
			{
				rwLock.ExitReadLock();
			}
		}
		#endregion
	}
}