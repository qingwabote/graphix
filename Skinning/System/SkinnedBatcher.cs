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
        private readonly struct SkinnedBatchKey : IEquatable<SkinnedBatchKey>
        {
            public readonly int Skin;

            public SkinnedBatchKey(int skin)
            {
                Skin = skin;
            }

            public override int GetHashCode()
            {
                return Skin;
            }

            public bool Equals(SkinnedBatchKey other)
            {
                return Skin == other.Skin;
            }
        }

        private readonly struct SkinnedBatchProgram : IBatchProgram<SkinnedBatchKey>
        {
            private static readonly int s_JOINTS = Shader.PropertyToID("_JointMap");

            public readonly int Skin;

            public readonly UnityObjectRef<Texture> Texture;

            public SkinnedBatchProgram(int skin, Texture texture)
            {
                Skin = skin;
                Texture = texture;
            }

            public SkinnedBatchKey GetBatchKey()
            {
                return new SkinnedBatchKey(Skin);
            }

            public void OnBatchCreated(ref Batch batch)
            {
                batch.PropertyTextureBind(s_JOINTS, Texture);
            }
        }

        private BatcherImpl<SkinnedBatchKey, SkinnedBatchProgram> m_Batcher;

        private int m_BatchEntry;

        public void OnCreate(ref SystemState state)
        {
            m_Batcher = new(Allocator.Persistent);
        }

        unsafe public void OnUpdate(ref SystemState state)
        {
            if (m_BatchEntry == 0)
            {
                m_BatchEntry = Profile.DefineEntry("SkinBatcher");
            }

            using (new Profile.Scope(m_BatchEntry))
            {
                var MaterialMeshInfoBuffered = SystemAPI.GetBufferTypeHandle<MaterialMeshInfoBuffered>(true);
                var LocalToWorld = SystemAPI.GetComponentTypeHandle<LocalToWorld>(true);
                var SkinInfo = SystemAPI.GetComponentTypeHandle<SkinInfo>(true);
                var MaterialMeshArray = SystemAPI.ManagedAPI.GetSharedComponentTypeHandle<MaterialMeshArray>();

                state.EntityManager.CompleteDependencyBeforeRO<LocalToWorld>();

                var skinArray = SkinArray.GetCurrent(state.EntityManager);

                using var scope = m_Batcher.MakeScope();
                foreach (var chunk in SystemAPI.QueryBuilder().WithAll<MaterialMeshInfoBuffered, SkinInfo, SkinArray>().Build().ToArchetypeChunkArray(Allocator.Temp))
                {
                    var materialMeshArray = chunk.GetSharedComponentIndex(MaterialMeshArray);
                    ref var queue = ref EntitiesGraphicsSystemUnmanaged.GetQueue(materialMeshArray);

                    var mp = new MaterialPropertyAccessor(ref state, chunk);
                    var mma = chunk.GetBufferAccessor(ref MaterialMeshInfoBuffered);
                    var worlds = chunk.GetNativeArray(ref LocalToWorld);
                    var SkinInfos = chunk.GetNativeArray(ref SkinInfo);
                    for (ushort entity = 0; entity < chunk.Count; entity++)
                    {
                        var mmb = mma[entity];
                        var mmp = (MaterialMeshInfo*)mmb.GetUnsafeReadOnlyPtr();
                        var skin = SkinInfos[entity];
                        var program = new SkinnedBatchProgram(skin.Skin, skinArray.GetCurrentStore(skin).Texture);
                        for (int i = 0; i < mmb.Length; i++)
                        {
                            scope.Merge(ref queue, materialMeshArray, mmp[i], mp.GetData(entity), worlds.ElementAtRO(entity).Value, program);
                        }
                    }
                }
            }
        }
    }
}