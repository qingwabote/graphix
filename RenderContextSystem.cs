using Bastard;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Transforms;

namespace Graphix
{
    public partial struct RenderContextSystem : ISystem
    {
        internal ComponentTypeHandle<LocalToWorld> LocalToWorld;
        internal SharedComponentTypeHandle<MaterialMeshArray> MaterialMeshArray;

        internal Bastard.UnsafeHashMap<int, UnsafeList<Batch>> m_Queues;

        public MaterialProperty.Cache MaterialPropertyCache;

        public unsafe ref UnsafeList<Batch> GetQueue(int materialMeshArray)
        {
            var queue = m_Queues.EnsureValuePtr(materialMeshArray, out var uninitialized);
            if (uninitialized)
            {
                *queue = new(32, Allocator.Temp);
            }
            return ref UnsafeUtility.AsRef<UnsafeList<Batch>>(queue);
        }

        public void OnCreate(ref SystemState state)
        {
            MaterialPropertyCache = new(state.EntityManager);

            LocalToWorld = state.GetComponentTypeHandle<LocalToWorld>(true);
            MaterialMeshArray = state.GetSharedComponentTypeHandle<MaterialMeshArray>();
        }

        public void OnDestroy(ref SystemState state)
        {
            MaterialPropertyCache.Dispose();
        }

        public void OnUpdate(ref SystemState state)
        {
            for (int i = 0; i < MaterialPropertyCache.Handles.Length; i++)
            {
                MaterialPropertyCache.Handles.ElementAt(i).Update(ref state);
            }

            LocalToWorld.Update(ref state);
            MaterialMeshArray.Update(ref state);

            m_Queues = new(2, Allocator.Temp);
        }
    }
}