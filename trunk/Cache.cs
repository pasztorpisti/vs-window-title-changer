using System;
using System.Collections.Generic;

namespace VSWindowTitleChanger
{
	class Cache<Key,Value>
	{
		public delegate Value CreateValueFromKey(Key key);

		public Cache(CreateValueFromKey value_creator)
		{
			m_ValueCreator = value_creator;
		}

		// Returns compile_error_default_value in case of a compile error.
		public Value GetEntry(Key key)
		{
			DateTime now = DateTime.Now;

			CacheEntry entry;
			if (m_CacheEntries.TryGetValue(key, out entry))
			{
				entry.last_access_time = now;
				DropExpiredEntries(now);
				return entry.val;
			}

			Value val = m_ValueCreator(key);
			m_CacheEntries[key] = new CacheEntry(val, now);
			DropExpiredEntries(now);
			return val;
		}

		private void DropExpiredEntries(DateTime now)
		{
			if (now - m_LastCleanupTime < m_CleanupPeriod)
				return;
			m_LastCleanupTime = now;

			List<Key> expired_keys = new List<Key>();
			foreach (KeyValuePair<Key, CacheEntry> entry in m_CacheEntries)
			{
				if (now - entry.Value.last_access_time >= m_ExpirationPeriod)
					expired_keys.Add(entry.Key);
			}

			foreach (Key key in expired_keys)
				m_CacheEntries.Remove(key);
		}

		class CacheEntry
		{
			public Value val;
			public DateTime last_access_time;

			public CacheEntry(Value val, DateTime last_access_time)
			{
				this.val = val;
				this.last_access_time = last_access_time;
			}
		}

		CreateValueFromKey m_ValueCreator;
		Dictionary<Key, CacheEntry> m_CacheEntries = new Dictionary<Key, CacheEntry>();
		DateTime m_LastCleanupTime = DateTime.Now;
		TimeSpan m_CleanupPeriod = new TimeSpan(0, 3, 0);
		TimeSpan m_ExpirationPeriod = new TimeSpan(0, 10, 0);
	}
}
