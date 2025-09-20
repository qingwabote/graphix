using Bastard;
using Unity.Entities;
using Unity.Transforms;
using UnityEngine;

namespace Graphix
{
    public partial struct SkinnedBatcher : ISystem
    {
        private static readonly int s_JOINTS = Shader.PropertyToID("_JointMap");
        private static readonly int s_OFFSET = Shader.PropertyToID("_JointOffset");

        private int m_BatchEntry;

        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<SkinArray>();

            m_BatchEntry = Profile.DefineEntry("SkinnedBatch");
        }

        public void OnUpdate(ref SystemState state)
        {
            using (new Profile.Scope(m_BatchEntry))
            {
                var skinArray = SkinArray.GetInstance(ref state);
                foreach (var (mmb, info, offset, world) in SystemAPI.Query<DynamicBuffer<MaterialMeshElement>, SkinInfo, SkinOffset, RefRO<LocalToWorld>>())
                {
                    for (int i = 0; i < mmb.Length; i++)
                    {
                        var mm = mmb[i];
                        var store = skinArray.GetStore(info);
                        if (Batch.Get(HashCode.Combine(mm.Mesh, mm.Material, store.Texture.GetHashCode()), out Batch batch))
                        {
                            batch.Material = mm.Material;
                            batch.Mesh = mm.Mesh;
                            batch.PropertyTextureBind(s_JOINTS, store.Texture);
                            batch.PropertyFloatAcquire(s_OFFSET);
                        }
                        batch.Worlds.Add(world.ValueRO.Value);
                        batch.PropertyFloatAdd(s_OFFSET, offset.Value);
                        batch.Count++;
                    }

                }
            }
        }
    }
}