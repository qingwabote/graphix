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

        private static TransientPool<List<float>> s_Floats;

        private int m_BatchEntry;

        public void OnCreate(ref SystemState state)
        {
            s_Floats = new();

            m_BatchEntry = Profile.DefineEntry("SkinnedBatch");
        }

        public void OnUpdate(ref SystemState state)
        {
            using (new Profile.Scope(m_BatchEntry))
            {
                foreach (var (mm, root) in SystemAPI.Query<MaterialMeshInfo, RefRO<SkinRootEntity>>())
                {
                    var Skin = state.EntityManager.GetComponentObject<SkinInfo>(root.ValueRO.Value);
                    if (Batch.Register(HashCode.Combine(mm.Mesh, mm.Material, Skin.Store.Texture.GetHashCode()), out Batch batch))
                    {
                        batch.Material = mm.Material;
                        batch.Mesh = mm.Mesh;
                        batch.MaterialProperty.Textures.Add(s_JOINTS, Skin.Store.Texture);
                        var floats = s_Floats.Get();
                        floats.Clear();
                        batch.MaterialProperty.Floats.Add(s_OFFSET, floats);
                    }
                    batch.InstanceWorlds.Add(state.EntityManager.GetComponentData<LocalToWorld>(root.ValueRO.Value).Value);
                    batch.MaterialProperty.Floats.TryGetValue(s_OFFSET, out var offsets);
                    offsets.Add(Skin.Offset);
                    batch.InstanceCount++;
                }
            }
        }
    }
}