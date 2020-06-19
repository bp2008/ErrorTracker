using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ErrorTrackerServer.Filtering
{
	/// <summary>
	/// Stores a dictionary which maps EventId to <typeparamref name="T"/>.  Can later be queried to return a dictionary that maps <typeparamref name="T"/> to List&lt;EventId&gt;
	/// </summary>
	/// <typeparam name="T"></typeparam>
	internal class ActionAggregator<T>
	{
		private Dictionary<long, T> eventIdToValue = new Dictionary<long, T>();

		/// <summary>
		/// Sets the value for the event in the dictionary.
		/// </summary>
		/// <param name="EventId"></param>
		/// <param name="value"></param>
		internal void Set(long EventId, T value)
		{
			eventIdToValue[EventId] = value;
		}

		/// <summary>
		/// Removes the event from the dictionary, returning true if the item was removed or false if it was not found.
		/// </summary>
		/// <param name="eventId"></param>
		/// <returns></returns>
		internal bool Remove(long eventId)
		{
			return eventIdToValue.Remove(eventId);
		}
		/// <summary>
		/// Builds a dictionary which maps values to a list of keys that had the value.
		/// </summary>
		/// <returns></returns>
		internal Dictionary<T, List<long>> ReverseMap()
		{
			Dictionary<T, List<long>> valueToEventIds = new Dictionary<T, List<long>>();
			foreach (KeyValuePair<long, T> kvp in eventIdToValue)
			{
				List<long> eventIds;
				if (!valueToEventIds.TryGetValue(kvp.Value, out eventIds))
					valueToEventIds[kvp.Value] = eventIds = new List<long>();
				eventIds.Add(kvp.Key);
			}
			return valueToEventIds;
		}

		/// <summary>
		/// Resets the internal state of this instance, making it reusable.
		/// </summary>
		internal void Clear()
		{
			eventIdToValue.Clear();
		}
	}
}
