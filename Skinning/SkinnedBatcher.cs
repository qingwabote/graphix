using System.Collections.Generic;
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
            m_BatchEntry = Profile.DefineEntry("SkinnedBatch");
        }

        public void OnUpdate(ref SystemState state)
        {
            using (new Profile.Scope(m_BatchEntry))
            {
                foreach (var (mmb, skin, world) in SystemAPI.Query<DynamicBuffer<MaterialMeshElement>, SkinInfo, RefRO<LocalToWorld>>())
                {
                    for (int i = 0; i < mmb.Length; i++)
                    {
                        var mm = mmb[i];
                        if (Batch.Get(HashCode.Combine(mm.Mesh, mm.Material, skin.Store.Texture.GetHashCode()), out Batch batch))
                        {
                            batch.Material = mm.Material;
                            batch.Mesh = mm.Mesh;
                            batch.PropertyTextureBind(s_JOINTS, skin.Store.Texture);
                            batch.PropertyFloatAcquire(s_OFFSET);
                        }
                        batch.Worlds.Add(world.ValueRO.Value);
                        batch.PropertyFloatAdd(s_OFFSET, skin.Offset);
                        batch.Count++;
                    }

                }
            }
        }
    }
}