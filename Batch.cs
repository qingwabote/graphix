using System.Runtime.CompilerServices;
using Bastard;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using static Unity.Collections.AllocatorManager;

namespace Graphix
{
    public struct Batch
    {
        public struct State
        {
            public int Index;
            public int MaxCount;
        }

        public NativeList<float4x4> LocalToWorlds;

        private FixedList32Bytes<int> m_PropNames;
        private FixedList128Bytes<ArrayUnsafeList<byte>> m_PropValues;

        private FixedList32Bytes<int> m_TextureNames;
        private FixedList32Bytes<UnityObjectRef<Texture>> m_TextureValues;

        public readonly int Material;
        public readonly int Mesh;

        public readonly int Count => LocalToWorlds.Length;
        public readonly bool PropertyAcquired => m_PropNames.Length > 0;

        public Batch(ref State state, int material, int mesh, AllocatorHandle allocator)
        {
            LocalToWorlds = new(state.MaxCount, allocator);

            m_PropNames = new();
            m_PropValues = new();

            m_TextureNames = new();
            m_TextureValues = new();

            Material = material;
            Mesh = mesh;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void PropertyTextureBind(int name, UnityObjectRef<Texture> texture)
        {
            m_TextureNames.Add(name);
            m_TextureValues.Add(texture);
        }

        public unsafe void PropertyDataAdd(ref State state, int name, byte* src, int count)
        {
            var index = m_PropNames.IndexOf(name);
            if (index == -1)
            {
                m_PropValues.Add(new(count == sizeof(float) ? ArrayType.Float : ArrayType.Vector, state.MaxCount * count));
                index = m_PropNames.Length;
                m_PropNames.Add(name);
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
            for (int i = 0; i < m_TextureNames.Length; i++)
            {
                output.SetTexture(m_TextureNames[i], m_TextureValues[i]);
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
            for (int i = 0; i < m_TextureNames.Length; i++)
            {
                output.SetTexture(m_TextureNames[i], m_TextureValues[i]);
            }

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
    }
}