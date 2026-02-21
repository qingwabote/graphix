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

        public int InitialCapacity;

        public NativeList<float4x4> LocalToWorlds = new(Allocator.Persistent);
        public int Count => LocalToWorlds.Length;

        private readonly Dictionary<int, Texture> m_Textures = new();

        private UnsafeHashMap<int, ArrayUnsafeList<byte>> m_PropData = new(8, Allocator.Persistent);

        public bool PropertyAcquired => m_PropData.Count > 0;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void PropertyTextureBind(int name, Texture texture)
        {
            m_Textures.Add(name, texture);
        }

        public unsafe void PropertyDataAdd(int name, byte* src, int count)
        {
            ref var list = ref m_PropData.EnsureValueRef(name, out var uninitialized);
            if (uninitialized)
            {
                list = new(count == sizeof(float) ? ArrayType.Float : ArrayType.Vector, InitialCapacity * count);
            }

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

            foreach (var kv in m_PropData)
            {
                ref var list = ref kv.Value;
                if (list.ArrayType == ArrayType.Float)
                {
                    output.SetFloatArray(kv.Key, (float[])ArrayAllocatorManaged.Get(list.Location));
                }
                else
                {
                    output.SetVectorArray(kv.Key, (Vector4[])ArrayAllocatorManaged.Get(list.Location));
                }
            }
        }

        public unsafe void PropertyToBlock(int index, MaterialPropertyBlock output)
        {
            foreach (var kv in m_PropData)
            {
                ref var list = ref kv.Value;
                if (list.ArrayType == ArrayType.Float)
                {
                    output.SetFloat(kv.Key, *(float*)(list.Ptr + index * sizeof(float)));
                }
                else
                {
                    output.SetVector(kv.Key, *(Vector4*)(list.Ptr + index * sizeof(Vector4)));
                }
            }
        }

        public void Clear()
        {
            LocalToWorlds.Clear();
            m_Textures.Clear();
            m_PropData.Clear();
        }
    }
}