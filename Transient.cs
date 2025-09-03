namespace Graphix
{
    using UnityEngine;

    public struct Transient<T>
    {
        private readonly T m_Reset;

        private int m_Version;

        private T m_Value;
        public T Value
        {
            get
            {
                if (m_Version != Time.frameCount)
                {
                    return m_Reset;
                }
                return m_Value;
            }

            set
            {
                m_Value = value;
                m_Version = Time.frameCount;
            }
        }

        public Transient(T value, T reset)
        {
            m_Value = value;
            m_Reset = reset;
            m_Version = Time.frameCount;
        }
    }
}