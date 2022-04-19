using System;

namespace PetaPoco.Core
{
	/// <summary>
	/// Combines an <see cref="IMapper"/> and a <see cref="System.Type"/> into one object for dictionary keying purposes.
	/// </summary>
	internal class PocoDataCacheKey
	{
		public readonly IMapper Mapper;
		public readonly Type Type;
		public PocoDataCacheKey(IMapper mapper, Type type)
		{
			Mapper = mapper;
			Type = type;
		}
		public override int GetHashCode()
		{
			return Mapper.GetHashCode() ^ Type.GetHashCode();
		}
		public override bool Equals(object obj)
		{
			if (obj is PocoDataCacheKey)
			{
				PocoDataCacheKey other = (PocoDataCacheKey)obj;
				return other.Mapper == Mapper && other.Type == Type;
			}
			return false;
		}
		public override string ToString()
		{
			return Mapper.ToString() + ", " + Type.ToString();
		}
	}
}
