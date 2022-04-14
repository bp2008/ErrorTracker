using PetaPoco;
using System.Collections.Concurrent;

namespace ErrorTrackerServer
{
	public abstract partial class DBBase
	{
		public static class PPMapper
		{
			private static ConcurrentDictionary<string, ConventionMapper> mappersBySchema = new ConcurrentDictionary<string, ConventionMapper>();
			public static IMapper Get(string schemaName)
			{
				return mappersBySchema.GetOrAdd(schemaName, sn =>
				{
					ConventionMapper m = new ConventionMapper();
					m.InflectTableName = (inflector, s) =>
					{
						return sn + "." + s.ToLower();
					};
					m.InflectColumnName = (inflector, s) =>
					{
						return s.ToLower();
					};
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
					return m;
				});
			}
		}
	}
}
