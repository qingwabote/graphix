using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Bastard;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

namespace Graphix
{
    public class Batch
    {
        public int Mesh;
        public int Material;

        public NativeList<float4x4> LocalToWorlds = new(Allocator.Persistent);
        public int Count => LocalToWorlds.Length;

        private readonly Dictionary<int, Texture> m_Textures = new();

        private FixedList32Bytes<int> m_PropNames = new();
        private FixedList128Bytes<ArrayUnsafeList<byte>> m_PropValues = new();

        public bool PropertyAcquired => m_PropValues.Length > 0;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void PropertyTextureBind(int name, Texture texture)
        {
            m_Textures.Add(name, texture);
        }

        public unsafe void PropertyDataAdd(int name, byte* src, int count)
        {
            var index = m_PropNames.IndexOf(name);
            if (index == -1)
            {
                index = m_PropNames.Length;
                m_PropNames.Add(name);
                m_PropValues.Add(new(count == sizeof(float) ? ArrayType.Float : ArrayType.Vector));
            }

            ref var list = ref m_PropValues.ElementAt(index);
            var d = Count * count - list.Length;
            if (d > 0)
            {
                list.Add(null, d);
            }
            list.Add(src, count);
        }

        public void PropertyToBlock(MaterialPropertyBlock output)
        {
            foreach (var (id, texture) in m_Textures)
            {
                output.SetTexture(id, texture);
            }

            for (int i = 0; i < m_PropNames.Length; i++)
            {
                ref var list = ref m_PropValues.ElementAt(i);
                if (list.ArrayType == ArrayType.Float)
                {
                    output.SetFloatArray(m_PropNames[i], (float[])ArrayAllocatorManaged.Get(list.Location));
                }
                else
                {
                    output.SetVectorArray(m_PropNames[i], (Vector4[])ArrayAllocatorManaged.Get(list.Location));
                }
            }
        }

        public unsafe void PropertyToBlock(int index, MaterialPropertyBlock output)
        {
            for (int i = 0; i < m_PropNames.Length; i++)
            {
                ref var list = ref m_PropValues.ElementAt(i);
                if (list.ArrayType == ArrayType.Float)
                {
                    output.SetFloat(m_PropNames[i], *(float*)(list.Ptr + index * sizeof(float)));
                }
                else
                {
                    output.SetVector(m_PropNames[i], *(Vector4*)(list.Ptr + index * sizeof(Vector4)));
                }
            }
        }

        public void Clear()
        {
            LocalToWorlds.Clear();
            m_Textures.Clear();
            m_PropNames.Clear();
            m_PropValues.Clear();
        }
    }
}