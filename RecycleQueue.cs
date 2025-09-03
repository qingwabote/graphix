using System;
using System.Collections;
using System.Collections.Generic;

namespace Graphix
{
    public class RecycleQueue<T> : IEnumerable<T> where T : new()
    {
        private readonly List<T> m_Data = new();
        public IReadOnlyList<T> Data => m_Data;

        private int m_Count = 0;
        public int Count => m_Count;

        private readonly Func<T> m_Create;

        public RecycleQueue(Func<T> create = null)
        {
            m_Create = create;
        }

        public T Push()
        {
            if (m_Data.Count == m_Count)
            {
                m_Data.Add(m_Create == null ? new T() : m_Create());
            }
            return m_Data[m_Count++];
        }

        public IEnumerator<T> GetEnumerator()
        {
            for (int i = 0; i < m_Count; i++)
            {
                yield return m_Data[i];
            }
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        public IEnumerable<T> Drain()
        {
            for (int i = 0; i < m_Count; i++)
            {
                yield return m_Data[i];
            }
            m_Count = 0;
        }
    }
}