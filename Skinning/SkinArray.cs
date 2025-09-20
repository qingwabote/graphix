using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Unity.Entities;

namespace Graphix
{
    public struct SkinArray : ISharedComponentData, IEquatable<SkinArray>
    {
        static private SkinArray s_Instance;

        static public SkinArray GetInstance(ref SystemState state)
        {
            if (s_Instance.HashCode != 0)
            {
                return s_Instance;
            }

            List<SkinArray> list = new();
            state.EntityManager.GetAllUniqueSharedComponentsManaged(list);
            s_Instance = list[1]; // 0 is always default
            return s_Instance;
        }

        public Skin[] Data;

        public int HashCode;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Skin.Store GetStore(SkinInfo info)
        {
            return info.Baking ? Data[info.Proto].Persistent : Data[info.Proto].Transient;
        }

        public override int GetHashCode()
        {
            return HashCode;
        }

        public bool Equals(SkinArray other)
        {
            return HashCode == other.HashCode;
        }
    }
}