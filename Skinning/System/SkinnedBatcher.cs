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
    [RequireMatchingQueriesForUpdate]
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

        private struct SkinnedBatchProgram : IBatchProgram<SkinnedBatchKey>
        {
            private static readonly int s_JOINTS = Shader.PropertyToID("_JointMap");

            public SkinArray SkinArray;

            public NativeArray<SkinInfo> SkinInfos;

            public SkinnedBatchKey KeyGen(int entity, MaterialMeshInfo mm)
            {
                return new SkinnedBatchKey
                {
                    Material = mm.Material,
                    Mesh = mm.Mesh,
                    Skin = SkinInfos[entity].Skin
                };
            }

            public void OnBatch(int entity, Batch batch)
            {
                batch.PropertyTextureBind(s_JOINTS, SkinArray.GetCurrentStore(SkinInfos[entity]).Texture);
            }
        }

        private int m_BatchEntry;

        unsafe public void OnUpdate(ref SystemState state)
        {
            if (m_BatchEntry == 0)
            {
                m_BatchEntry = Profile.DefineEntry("SkinnedBatcher");
            }

            using (new Profile.Scope(m_BatchEntry))
            {
                var MaterialMeshElement = SystemAPI.GetBufferTypeHandle<MaterialMeshElement>(true);
                var LocalToWorld = SystemAPI.GetComponentTypeHandle<LocalToWorld>(true);
                var SkinInfo = SystemAPI.GetComponentTypeHandle<SkinInfo>(true);
                var MaterialMeshArray = SystemAPI.ManagedAPI.GetSharedComponentTypeHandle<MaterialMeshArray>();

                state.EntityManager.CompleteDependencyBeforeRO<LocalToWorld>();

                var program = new SkinnedBatchProgram();
                program.SkinArray = SkinArray.GetCurrent(state.EntityManager);

                var batcher = new BatcherImpl<SkinnedBatchKey, SkinnedBatchProgram>(MaterialMeshArray, 128);
                foreach (var chunk in SystemAPI.QueryBuilder().WithAll<MaterialMeshElement, SkinInfo, SkinArray>().Build().ToArchetypeChunkArray(Allocator.Temp))
                {
                    batcher.BeginChunk(ref state, chunk);
                    var mma = chunk.GetBufferAccessor(ref MaterialMeshElement);
                    var worlds = chunk.GetNativeArray(ref LocalToWorld);
                    program.SkinInfos = chunk.GetNativeArray(ref SkinInfo);
                    for (int entity = 0; entity < chunk.Count; entity++)
                    {
                        var mmb = mma[entity];
                        var mmp = (MaterialMeshInfo*)mmb.GetUnsafeReadOnlyPtr();
                        for (int i = 0; i < mmb.Length; i++)
                        {
                            batcher.Add(entity, worlds.ElementAtRO(entity).Value, mmp[i], ref program);
                        }
                    }
                    batcher.EndChunk();
                }
            }
        }
    }
}