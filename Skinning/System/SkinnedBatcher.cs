using Bastard;
using Unity.Collections;
using Unity.Entities;
using Unity.Transforms;
using Unity.Rendering;
using System;
using UnityEngine;

namespace Graphix
{
    [UpdateInGroup(typeof(BatchGroup))]
    public partial struct SkinnedBatcher : ISystem
    {
        private struct SkinnedBatchKey : IEquatable<SkinnedBatchKey>
        {
            public int Material;
            public int Mesh;
            public int Skin;

            public override int GetHashCode()
            {
                return Bastard.HashCode.Combine(Material, Mesh, Skin);
            }

            public bool Equals(SkinnedBatchKey other)
            {
                return Material == other.Material && Mesh == other.Mesh && Skin == other.Skin;
            }
        }

        private struct SkinnedBatchSorter : IBatchSorter<SkinnedBatchKey, SkinInfo>
        {
            private static readonly int s_JOINTS = Shader.PropertyToID("_JointMap");

            public SkinArray SkinArray;

            public SkinnedBatchKey KeyGen(MaterialMeshInfo mm, SkinInfo skin)
            {
                return new SkinnedBatchKey
                {
                    Material = mm.Material,
                    Mesh = mm.Mesh,
                    Skin = skin.Skin
                };
            }

            public void BatchInit(Batch batch, MaterialMeshInfo mm, SkinInfo skin)
            {
                batch.Material = mm.Material;
                batch.Mesh = mm.Mesh;
                batch.PropertyTextureBind(s_JOINTS, SkinArray.GetCurrentStore(skin).Texture);
            }
        }

        private static BatcherImpl<SkinnedBatchKey, SkinnedBatchSorter, SkinInfo> s_Batcher = new();

        private int m_BatchEntry;

        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<SkinInfo>();
            state.RequireForUpdate<SkinArray>();
        }

        unsafe public void OnUpdate(ref SystemState state)
        {
            if (m_BatchEntry == 0)
            {
                m_BatchEntry = Profile.DefineEntry("SkinnedBatcher");
            }

            using (new Profile.Scope(m_BatchEntry))
            {
                var MaterialMeshElement = SystemAPI.GetBufferTypeHandle<MaterialMeshElement>(true);
                MaterialMeshElement.Update(ref state);
                var LocalToWorld = SystemAPI.GetComponentTypeHandle<LocalToWorld>(true);
                LocalToWorld.Update(ref state);
                var SkinInfo = SystemAPI.GetComponentTypeHandle<SkinInfo>(true);
                SkinInfo.Update(ref state);

                state.EntityManager.CompleteDependencyBeforeRO<LocalToWorld>();

                s_Batcher.Sorter.SkinArray = SkinArray.GetCurrent(state.EntityManager);
                foreach (var chunk in SystemAPI.QueryBuilder().WithAll<MaterialMeshElement>().Build().ToArchetypeChunkArray(Allocator.Temp))
                {
                    s_Batcher.BeginChunk(ref state, chunk);
                    var mma = chunk.GetBufferAccessor(ref MaterialMeshElement);
                    var worlds = chunk.GetNativeArray(ref LocalToWorld);
                    var skins = chunk.GetNativeArray(ref SkinInfo);
                    for (int entity = 0; entity < chunk.Count; entity++)
                    {
                        var mmb = mma[entity];
                        var mmp = (MaterialMeshInfo*)mmb.GetUnsafeReadOnlyPtr();
                        for (int i = 0; i < mmb.Length; i++)
                        {
                            s_Batcher.Add(entity, worlds.ElementAtRO(entity).Value, mmp[i], skins[entity]);
                        }
                    }
                    s_Batcher.EndChunk();
                }
                s_Batcher.Clear();
            }
        }
    }
}