using System.Collections.Generic;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Rendering;
using UnityEngine;

namespace Graphix
{
    public readonly struct MaterialProperty
    {
        struct PropertyComparer : IComparer<MaterialProperty>
        {
            public int Compare(MaterialProperty x, MaterialProperty y)
            {
                return x.Name.CompareTo(y.Name);
            }
        }

        public const int Capacity = 7;

        private struct HandlesTag { }
        static public readonly SharedStatic<NativeArray<DynamicComponentTypeHandle>> Handles = SharedStatic<NativeArray<DynamicComponentTypeHandle>>.GetOrCreate<HandlesTag>();

        private struct TypeToPropertyTag { }
        // use TypeIndex of ComponentType as key, ignore AccessModeType
        static private readonly SharedStatic<UnsafeHashMap<int, MaterialProperty>> s_TypeToProperty = SharedStatic<UnsafeHashMap<int, MaterialProperty>>.GetOrCreate<TypeToPropertyTag>();

        private struct ArchetypeToPropertyTag { }
        static private readonly SharedStatic<UnsafeHashMap<EntityArchetype, UnsafeList<MaterialProperty>.ReadOnly>> s_ArchetypeToProperty = SharedStatic<UnsafeHashMap<EntityArchetype, UnsafeList<MaterialProperty>.ReadOnly>>.GetOrCreate<ArchetypeToPropertyTag>();

        static public UnsafeList<MaterialProperty>.ReadOnly Get(EntityArchetype archetype)
        {
            if (s_ArchetypeToProperty.Data.TryGetValue(archetype, out var output))
            {
                return output;
            }

            var types = archetype.GetComponentTypes(Allocator.Temp);

            int count = 0;
            foreach (var type in types)
            {
                if (s_TypeToProperty.Data.ContainsKey(type.TypeIndex))
                    count++;
            }
            Debug.Assert(count <= Capacity);

            UnsafeList<MaterialProperty> properties = new(count, Allocator.Persistent);
            foreach (var type in types)
            {
                if (s_TypeToProperty.Data.TryGetValue(type.TypeIndex, out MaterialProperty property))
                    properties.Add(property);
            }
            NativeSortExtension.Sort(properties, new PropertyComparer());

            s_ArchetypeToProperty.Data.Add(archetype, output = properties.AsReadOnly());

            return output;
        }

        static public void Initialize(EntityManager entityManager)
        {
            s_TypeToProperty.Data = new(8, Allocator.Persistent);
            List<DynamicComponentTypeHandle> handles = new();
            foreach (var typeInfo in TypeManager.AllTypes)
            {
                var type = typeInfo.Type;

                if (typeof(IComponentData).IsAssignableFrom(type) || typeof(IBufferElementData).IsAssignableFrom(type))
                {
                    var attributes = type.GetCustomAttributes(typeof(MaterialPropertyAttribute), false);
                    if (attributes.Length > 0)
                    {
                        var attribute = (MaterialPropertyAttribute)attributes[0];
                        var property = new MaterialProperty(Shader.PropertyToID(attribute.Name), handles.Count, UnsafeUtility.SizeOf(type), typeInfo.TypeIndex.IsBuffer);
                        handles.Add(entityManager.GetDynamicComponentTypeHandle(ComponentType.ReadOnly(typeInfo.TypeIndex)));
                        s_TypeToProperty.Data.Add(typeInfo.TypeIndex, property);
                    }
                }
            }
            Handles.Data = handles.ToNativeArray(Allocator.Persistent);

            s_ArchetypeToProperty.Data = new(8, Allocator.Persistent);
        }


        public readonly int Name;

        private readonly int m_Type;
        public int TypeIndex => m_Type >> 9;
        public int TypeSize => m_Type >> 1 & 0xFF;
        public bool TypeIsBuffer => (m_Type & 0x1) != 0;

        public MaterialProperty(int name, int typeIndex, int typeSize, bool typeIsBuffer)
        {
            Name = name;
            m_Type = (typeIndex << 9) | (typeSize << 1) | (typeIsBuffer ? 1 : 0);
        }
    }
}