using RepoDb;
using RepoDb.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ErrorTrackerServer.Database.Project.v2
{
	/// <summary>
	/// Allows uint properties to exist in database models by casting them to and from int.
	/// </summary>
	public class UIntPropertyHandler : IPropertyHandler<int, uint>
	{
		public uint Get(int input, ClassProperty property)
		{
			return (uint)input;
		}

		public int Set(uint input, ClassProperty property)
		{
			return (int)input;
		}
	}
}
