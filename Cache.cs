using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace VSWindowTitleChanger
{
	// A cache that keeps at most N items and drops the oldest unused item if necessary.
	class Cache<Key,Value>
	{
		public delegate Value CreateValueFromKey(Key key);

		public Cache(CreateValueFromKey value_creator, int max_size)
		{
			Debug.Assert(max_size > 0);
			m_ValueCreator = value_creator;
			m_MaxSize = max_size;
		}

		public Value GetEntry(Key key)
		{
			CacheEntry entry;
			if (m_CacheEntries.TryGetValue(key, out entry))
			{
				m_EntryKeys.Remove(entry.list_node);
				m_EntryKeys.AddLast(entry.list_node);
				return entry.val;
			}

			Debug.Assert(m_CacheEntries.Count <= m_MaxSize);
			if (m_CacheEntries.Count >= m_MaxSize)
			{
				m_CacheEntries.Remove(m_EntryKeys.First.Value);
				m_EntryKeys.RemoveFirst();
			}
			LinkedListNode<Key> list_node = new LinkedListNode<Key>(key);
			Value val = m_ValueCreator(key);
			m_CacheEntries[key] = new CacheEntry(val, list_node);
			m_EntryKeys.AddLast(list_node);
			return val;
		}

		class CacheEntry
		{
			public Value val;
			public LinkedListNode<Key> list_node;

			public CacheEntry(Value val, LinkedListNode<Key> list_node)
			{
				this.val = val;
				this.list_node = list_node;
			}
		}

		CreateValueFromKey m_ValueCreator;
		int m_MaxSize;
		Dictionary<Key, CacheEntry> m_CacheEntries = new Dictionary<Key, CacheEntry>();
		LinkedList<Key> m_EntryKeys = new LinkedList<Key>();
	}
}
