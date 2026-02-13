using System;
using System.Runtime.InteropServices;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Entities.LowLevel.Unsafe;

namespace Graphix
{
    public unsafe ref struct MaterialPropertyData
    {
        private readonly ushort m_Entity;
        private readonly ushort m_Element;
        private readonly MaterialPropertyAccessor* m_Accessor;

        private int m_Index;

        internal MaterialPropertyData(MaterialPropertyAccessor* accessor, ushort entity, ushort element)
        {
            m_Entity = entity;
            m_Element = element;
            m_Accessor = accessor;

            m_Index = -1;
        }

        public readonly MaterialPropertyData GetEnumerator()
        {
            return this;
        }

        public bool MoveNext()
        {
            int next = m_Index + 1;
            if (next >= m_Accessor->Properties.Length)
                return false;

            m_Index = next;
            return true;
        }

        public (int, int, ulong) Current
        {
            get
            {
                var property = m_Accessor->Properties.Ptr[m_Index];
                if (property.TypeIsBuffer)
                {
                    void* ptr;
#if ENABLE_UNITY_COLLECTIONS_CHECKS
                    ptr = m_Accessor->ComponentData[m_Index].BufferAccessor.GetUnsafeReadOnlyPtrAndLength(m_Entity, out var length);
                    if (m_Element < 0 || m_Element >= length)
                    {
                        throw new IndexOutOfRangeException($"Index {m_Element} is out of range of '{length}' Length.");
                    }
#else
                    ptr = m_Accessor->ComponentData[m_Index].BufferAccessor.GetUnsafeReadOnlyPtr(m_Entity);
#endif
                    return (property.Name, property.TypeSize, (ulong)((byte*)ptr + m_Element * property.TypeSize));
                }
                else
                {
                    return (property.Name, property.TypeSize, (ulong)((byte*)m_Accessor->ComponentData[m_Index].Ptr + m_Entity * property.TypeSize));
                }
            }
        }
    }

    [StructLayout(LayoutKind.Explicit)]
    public unsafe struct MaterialPropertyComponentData
    {
        [FieldOffset(0)]
        public void* Ptr;
        [FieldOffset(0)]
        public UnsafeUntypedBufferAccessor BufferAccessor;
    }

    public unsafe readonly ref struct MaterialPropertyAccessor
    {
        internal readonly UnsafeList<MaterialProperty>.ReadOnly Properties;

        internal readonly FixedList512Bytes<MaterialPropertyComponentData> ComponentData;

        public MaterialPropertyAccessor(ref SystemState state, ArchetypeChunk chunk)
        {
            Properties = MaterialProperty.Get(chunk.Archetype);
            ComponentData = new();
            for (int i = 0; i < Properties.Length; i++)
            {
                var property = Properties.Ptr[i];
                ref var handle = ref MaterialProperty.Handles[property.TypeIndex];
                handle.Update(ref state);
                if (property.TypeIsBuffer)
                {
                    ComponentData.Add(new() { BufferAccessor = chunk.GetUntypedBufferAccessor(ref handle) });
                }
                else
                {
                    ComponentData.Add(new() { Ptr = chunk.GetDynamicComponentDataArrayReinterpret<byte>(ref handle, property.TypeSize).GetUnsafeReadOnlyPtr() });
                }
            }
        }

        public MaterialPropertyData GetData(ushort entity, ushort element = 0)
        {
            fixed (MaterialPropertyAccessor* p = &this)
            {
                return new MaterialPropertyData(p, entity, element);
            }
            // return new MaterialPropertyData((MaterialPropertyAccessor *)UnsafeUtility.AddressOf(ref Properties), entity, element);
        }
    }
}