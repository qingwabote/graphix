using Unity.Collections;

namespace Graphix
{
    public abstract class MemoryView<T> where T : unmanaged
    {
        protected NativeReference<NativeArray<T>> m_Source = new(Allocator.Persistent);

        public ref NativeReference<NativeArray<T>> Source => ref m_Source;

        private int m_Length;
        public int Length => m_Length;

        private bool m_Invalidated = false;

        public MemoryView(int length)
        {
            m_Length = length;
        }

        public int AddBlock(int length)
        {
            var offset = m_Length;
            Resize(offset + length);
            m_Invalidated = true;
            return offset;
        }

        public void Update()
        {
            if (m_Invalidated)
            {
                Upload();
                m_Invalidated = false;
            }
        }

        public void Resize(int length)
        {
            Reserve(length);
            m_Length = length;
        }

        public void Reset()
        {
            m_Length = 0;
        }

        protected abstract void Reserve(int capacity);

        protected abstract void Upload();
    }
}