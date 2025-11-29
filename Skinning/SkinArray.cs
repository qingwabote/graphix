using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Unity.Entities;

namespace Graphix
{
    public struct SkinArray : ISharedComponentData, IEquatable<SkinArray>
    {
        static private List<SkinArray> s_Instances = new();

        static public SkinArray GetCurrent(EntityManager entityManager)
        {
            s_Instances.Clear();
            entityManager.GetAllUniqueSharedComponentsManaged(s_Instances);
            return s_Instances[1];
        }

        public Skin[] Data;

        private int m_HashCode;

        internal SkinArray(Skin[] data, int hashCode)
        {
            Data = data;
            m_HashCode = hashCode;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Skin.Store GetCurrentStore(SkinInfo info)
        {
            return info.Baking ? Data[info.Skin].Persistent : Data[info.Skin].Transient;
        }

        public override int GetHashCode()
        {
            return m_HashCode;
        }

        public bool Equals(SkinArray other)
        {
            return m_HashCode == other.m_HashCode;
        }
    }
}