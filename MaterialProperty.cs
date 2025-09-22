using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Rendering;
using UnityEngine;

namespace Graphix
{
    public readonly struct MaterialProperty
    {
        static public DynamicComponentTypeHandle[] Handles;

        // use TypeIndex of ComponentType as key, ignore AccessModeType
        static private UnsafeHashMap<int, MaterialProperty> s_TypeToProperty;

        static private UnsafeHashMap<EntityArchetype, UnsafeList<MaterialProperty>.ReadOnly> s_ArchetypeToProperty;

        static public UnsafeList<MaterialProperty>.ReadOnly Get(EntityArchetype archetype)
        {
            if (s_ArchetypeToProperty.TryGetValue(archetype, out var output))
            {
                return output;
            }

            var types = archetype.GetComponentTypes(Allocator.Temp);

            int count = 0;
            foreach (var type in types)
            {
                if (s_TypeToProperty.ContainsKey(type.TypeIndex))
                    count++;
            }

            UnsafeList<MaterialProperty> properties = new(count, Allocator.Persistent);
            foreach (var type in types)
            {
                if (s_TypeToProperty.TryGetValue(type.TypeIndex, out MaterialProperty property))
                    properties.Add(property);
            }

            s_ArchetypeToProperty.Add(archetype, output = properties.AsReadOnly());

            return output;
        }

        static public void Initialize(EntityManager entityManager)
        {
            s_TypeToProperty = new(8, Allocator.Persistent);
            List<DynamicComponentTypeHandle> handles = new();
            foreach (var typeInfo in TypeManager.AllTypes)
            {
                var type = typeInfo.Type;

                bool isComponent = typeof(IComponentData).IsAssignableFrom(type);
                if (isComponent)
                {
                    var attributes = type.GetCustomAttributes(typeof(MaterialPropertyAttribute), false);
                    if (attributes.Length > 0)
                    {
                        var attribute = (MaterialPropertyAttribute)attributes[0];
                        var property = new MaterialProperty(handles.Count, Shader.PropertyToID(attribute.Name), (short)UnsafeUtility.SizeOf(type));
                        handles.Add(entityManager.GetDynamicComponentTypeHandle(ComponentType.ReadOnly(typeInfo.TypeIndex)));
                        s_TypeToProperty.Add(typeInfo.TypeIndex, property);
                    }
                }
            }
            Handles = handles.ToArray();

            s_ArchetypeToProperty = new(8, Allocator.Persistent);
        }


        public readonly int Type;
        public readonly int Name;
        public readonly short Size;

        public MaterialProperty(int type, int name, short size)
        {
            Type = type;
            Name = name;
            Size = size;
        }
    }
}