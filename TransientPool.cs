using System;
using System.Collections.Generic;

namespace Graphix
{
    public class TransientPool<T> where T : new()
    {
        private readonly List<T> m_Data = new();
        public IReadOnlyList<T> Data => m_Data;

        private Transient<int> m_Count = new(0, 0);
        public int Count => m_Count.Value;

        private readonly Func<T> m_Creator;

        public TransientPool(Func<T> creator = null)
        {
            m_Creator = creator;
        }

        public T Get()
        {
            if (m_Data.Count == m_Count.Value)
            {
                m_Data.Add(m_Creator == null ? new T() : m_Creator());
            }
            return m_Data[m_Count.Value++];
        }
    }
}