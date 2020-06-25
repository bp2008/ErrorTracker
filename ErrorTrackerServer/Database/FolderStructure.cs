using BPUtil;
using ErrorTrackerServer.Database.Model;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.Serialization;

namespace ErrorTrackerServer
{
	/// <summary>
	/// Represents one node in a directory tree. The project's root FolderId is always 0.
	/// </summary>
	public class FolderStructure
	{
		/// <summary>
		/// ID of the folder represented by this instance. The root's ID is 0.
		/// </summary>
		public int FolderId { get; private set; }
		/// <summary>
		/// Name of the folder represented by this instance.
		/// </summary>
		public string Name { get; private set; }
		/// <summary>
		/// ID of the folder which is the parent of this one. The root's parent ID is itself, 0.
		/// </summary>
		[JsonIgnore]
		private int ParentFolderId;
		/// <summary>
		/// A list of folder nodes that are children of this node. Do not edit this list outside of the FolderStructure constructor.
		/// </summary>
		public List<FolderStructure> Children { get; private set; } = new List<FolderStructure>();
		/// <summary>
		/// The parent of this instance. The root's parent is null.
		/// </summary>
		[JsonIgnore]
		public FolderStructure Parent { get; private set; }

		/// <summary>
		/// A map of FolderId to all FolderStructure instances contained in this tree.
		/// </summary>
		[JsonIgnore]
		private Dictionary<int, FolderStructure> folderMap;

		/// <summary>
		/// Gets the absolute path of this folder. Empty string only if there is a circular reference (no path to root).
		/// </summary>
		public string AbsolutePath
		{
			get
			{
				return GetPath();
			}
		}

		public static FolderStructure Build(List<Folder> allFolders)
		{
			// Construct FolderStructure instances and organize them by FolderId.
			Dictionary<int, FolderStructure> folderMap = new Dictionary<int, FolderStructure>(allFolders.Count);
			foreach (Folder f in allFolders)
				folderMap.Add(f.FolderId, new FolderStructure(f));

			// Populate Children lists, Parent references, and folderMap references.
			foreach (FolderStructure f in folderMap.Values)
			{
				f.folderMap = folderMap;
				if (f.FolderId != 0) // For all except the root node
				{
					// Get this node's parent
					if (!folderMap.TryGetValue(f.ParentFolderId, out FolderStructure parent))
						throw new Exception("Folder with ID " + f.FolderId + " has nonexistent parent " + f.ParentFolderId);
					f.Parent = parent; // Assign this node's parent property
					parent.Children.Add(f); // Assign this node as a child of its parent
				}
			}

			// Ensure that every folder has a path to root
			foreach (FolderStructure f in folderMap.Values)
				if (!f.HasPathToRoot(out HashSet<long> nodesOnRootPath))
					throw new FolderCircularReferenceException(f.FolderId);

			// Sort Children lists by name.
			foreach (FolderStructure f in folderMap.Values)
				f.SortChildren();

			return folderMap[0];
		}
		private FolderStructure(Folder f)
		{
			this.FolderId = f.FolderId;
			this.Name = f.Name;
			this.ParentFolderId = f.ParentFolderId;
		}

		/// <summary>
		/// Tries to get the folder with the specified ID from anywhere in the tree. Returns true if successful.
		/// </summary>
		/// <param name="folderId">ID of the folder to retrieve.</param>
		/// <param name="node"></param>
		/// <returns></returns>
		public bool TryGetNode(int folderId, out FolderStructure node)
		{
			return folderMap.TryGetValue(folderId, out node);
		}
		/// <summary>
		/// Gets the child node with the specified name (case-insensitive). May return null.
		/// </summary>
		/// <param name="folderName">Gets the child node with the specified name (case-insensitive).</param>
		/// <returns></returns>
		public FolderStructure GetChild(string folderName)
		{
			return Children.FirstOrDefault(f => f.Name.Equals(folderName, StringComparison.OrdinalIgnoreCase));
		}
		/// <summary>
		/// Returns true if this folder has a path to the root by looking at Parent folders. Also true if this folder is the root.
		/// </summary>
		/// <returns></returns>
		public bool HasPathToRoot(out HashSet<long> nodesOnRootPath)
		{
			nodesOnRootPath = new HashSet<long>();
			Stack<string> pathParts = new Stack<string>();
			return FindRootPath(nodesOnRootPath, ref pathParts);
		}
		/// <summary>
		/// Recursively finds the root path while preventing circular references.
		/// </summary>
		/// <param name="idsOnPath">A collection of Folder IDs that have been visited while traversing the folder structure.</param>
		/// <returns></returns>
		private bool FindRootPath(HashSet<long> idsOnPath, ref Stack<string> pathParts)
		{
			if (Parent == null)
				return FolderId == 0; // This "should" always return true.
			else if (idsOnPath.Contains(FolderId))
				return false; // This indicates a circular reference was found; thus this element is contained within itself.
			else
			{
				idsOnPath.Add(FolderId);
				if (pathParts != null)
					pathParts.Push(Name);
				return Parent.FindRootPath(idsOnPath, ref pathParts);
			}
		}

		/// <summary>
		/// Resolves the specified path relative to this folder, returning the specified folder or null if the path is valid but cannot be resolved.
		/// Paths beginning with '/' are absolute paths resolved from the root. For the purpose of path resolving, the root folder has no name.
		/// Some error conditions can cause this method to throw an exception.
		/// </summary>
		/// <param name="path">Path to resolve, e.g. "/" or "/stuff/here" or "/stuff/here/"</param>
		/// <param name="dbInTransaction">The database where this FolderStructure was loaded from, in which the calling code has started a transaction. Provide this if you want missing path elements to be created automatically. This argument should be null unless being called from within a DB instance with the intent to create folders.</param>
		/// <returns></returns>
		public FolderStructure ResolvePath(string path, DB dbInTransaction)
		{
			if (string.IsNullOrWhiteSpace(path))
				return this;

			FolderStructure next = null;

			string[] parts = path.Split('/');
			if (parts[0] == "")
				TryGetNode(0, out next);
			else if (parts[0] == "..")
			{
				if (Parent == null)
					next = this;
				else
					next = Parent;
			}
			else
			{
				foreach (FolderStructure child in Children)
				{
					if (child.Name.Equals(parts[0], StringComparison.OrdinalIgnoreCase))
					{
						next = child;
						break;
					}
				}
				if (next == null && dbInTransaction != null)
				{
					if (!dbInTransaction.IsInTransaction)
						Logger.Info("FolderStructure.ResolvePath was given a DB instance that is not in a transaction. At " + new StackTrace(true).ToString());

					// Create Folder					
					if (dbInTransaction.AddFolder(parts[0], FolderId, out string errorMessage, out Folder newFolder))
					{
						next = new FolderStructure(newFolder);
						next.Parent = this;
						folderMap.Add(next.FolderId, next);
						Children.Add(next);
						SortChildren();
					}
					else
						throw new Exception("Unable to create folder \"" + parts[0] + "\" because of error: " + errorMessage);
				}
			}

			if (parts.Length > 2 && string.IsNullOrWhiteSpace(parts[1]))
				throw new Exception("Empty/whitespace path element is invalid"); // Next path element is empty/whitespace and would cause our parser to restart at the root node if we didn't catch it here. e.g. "//" or "stuff//here" or "stuff/  /here/"

			if (next == null)
				return null;

			return next.ResolvePath(string.Join("/", parts.Skip(1)), dbInTransaction);
		}

		/// <summary>
		/// Gets the absolute path of this folder. Empty string only if there is a circular reference (no path to root).
		/// </summary>
		/// <returns></returns>
		private string GetPath()
		{
			HashSet<long> nodesOnRootPath = new HashSet<long>();
			Stack<string> pathParts = new Stack<string>();
			if (!FindRootPath(nodesOnRootPath, ref pathParts))
				return "";
			return "/" + string.Join("/", pathParts);
		}
		/// <summary>
		/// Sorts the children of this folder alphabetically.
		/// </summary>
		private void SortChildren()
		{
			Children.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase));
		}
		/// <summary>
		/// Gets the absolute path of this folder. Empty string only if there is a circular reference (no path to root).
		/// </summary>
		/// <returns></returns>
		public override string ToString()
		{
			return GetPath();
		}
	}

	[Serializable]
	internal class FolderCircularReferenceException : Exception
	{
		/// <summary>
		/// ID of the folder which was identified as having a circular reference.  If this folder is moved into the root, the circular reference will be resolved.
		/// </summary>
		public int FolderId;

		public FolderCircularReferenceException(int folderId) : base("Folder with ID " + folderId + " has a circular reference in its path to root.")
		{
			this.FolderId = folderId;
		}
	}
}