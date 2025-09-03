using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

namespace Graphix
{
    public class TextureView : MemoryView<float>
    {
        private static (int width, int height) Length2extent(int length)
        {
            float texels = math.ceil(length / 4f);
            int width = 1 << (int)math.ceil(math.log2(math.ceil(math.sqrt(texels))));
            int height = 1 << (int)math.ceil(math.log2(math.ceil(texels / width)));
            return (width, height);
        }

        public readonly Texture2D Texture;

        public TextureView(int length = 0, int capacity = 16) : base(length)
        {
            var (width, height) = Length2extent(math.max(length, capacity));
            Texture = new Texture2D(width, height, TextureFormat.RGBAFloat, false, true);
            m_Source.Value = Texture.GetPixelData<float>(0);
        }

        protected override void Reserve(int capacity)
        {
            var (width, height) = Length2extent(capacity);
            if (Texture.width * Texture.height >= width * height)
            {
                return;
            }

            var copy = new NativeArray<float>(Length, Allocator.Temp);
            NativeArray<float>.Copy(m_Source.Value, 0, copy, 0, Length);
            if (!Texture.Reinitialize(width, height))
            {
                Debug.Log("Texture.Reinitialize failed!");
                return;
            }
            m_Source.Value = Texture.GetPixelData<float>(0);
            NativeArray<float>.Copy(copy, 0, m_Source.Value, 0, Length);
        }

        protected override void Upload()
        {
            Texture.Apply(false);
        }
    }
}