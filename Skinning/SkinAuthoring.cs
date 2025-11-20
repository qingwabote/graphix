using System;
using System.Collections.Generic;
using Bastard;
using Unity.Collections;
using Unity.Entities;
using Unity.Rendering;
using Unity.Transforms;
using UnityEngine;

namespace Graphix
{
    public class SkinAuthoring : MonoBehaviour
    {
        public Skin Skin;
        public bool Baking;
    }

    [TemporaryBakingType]
    public class SkinInfoBaking : IComponentData
    {
        public Skin Skin;
        public bool Baking;
    }

    public struct SkinInfo : IComponentData
    {
        public int Skin;
        public bool Baking;
        public BlobAssetReference<JointMeta> JointMeta;
    }

    public struct SkinNode : IBufferElementData
    {
        public Entity Target;
        public int Parent;
    }

    [TemporaryBakingType]
    public struct TransformBaking : IBufferElementData
    {
        public LocalTransform Value;
    }

    public unsafe struct JointSource : IComponentData
    {
        private long m_Value;

        public NativeArray<float>* Value
        {
            get => (NativeArray<float>*)m_Value;
            set => m_Value = (long)value;
        }

        public JointSource(NativeArray<float>* value)
        {
            m_Value = (long)value;
        }
    }

    [MaterialProperty("_JointOffset")]
    public struct JointOffset : IComponentData
    {
        public float Value;
    }

    class SkinBaker : Baker<SkinAuthoring>
    {
        public override void Bake(SkinAuthoring authoring)
        {
            var entity = GetEntity(TransformUsageFlags.Dynamic);

            var nodes = AddBuffer<SkinNode>(entity);
            nodes.ResizeUninitialized(authoring.Skin.Nodes.Length);
            var tansforms = AddBuffer<TransformBaking>(entity);
            tansforms.ResizeUninitialized(authoring.Skin.Nodes.Length);
            for (int i = 0; i < nodes.Length; i++)
            {
                var path = authoring.Skin.Nodes[i];
                var target = authoring.transform.GetChildByPath(path);
                if (!target)
                {
                    throw new Exception($"{path} not exists");
                }
                int parent = -1;
                {
                    int index = path.LastIndexOf('/');
                    if (index > -1)
                    {
                        var p = path.Substring(0, index);
                        for (parent = i - 1; parent > -1; parent--)
                        {
                            if (authoring.Skin.Nodes[parent] == p)
                            {
                                break;
                            }
                        }
                    }
                }
                nodes[i] = new SkinNode
                {
                    Target = GetEntity(target, TransformUsageFlags.None),
                    Parent = parent
                };
                tansforms[i] = new TransformBaking { Value = LocalTransform.FromPositionRotationScale(target.localPosition, target.localRotation, target.localScale.x) };
            }
            AddComponentObject(entity, new SkinInfoBaking
            {
                Skin = authoring.Skin,
                Baking = authoring.Baking,
            });
            AddComponent<JointSource>(entity);
            AddComponent<JointOffset>(entity);

            // "The transform of the node that the skin is attached to is ignored", we use a single entity to hold all MaterialMeshes. https://github.khronos.org/glTF-Tutorials/gltfTutorial/gltfTutorial_020_Skins.html
            var materails = new List<Material>();
            var meshes = new List<Mesh>();
            var renderers = GetComponentsInChildren<SkinnedMeshRenderer>();
            foreach (var renderer in renderers)
            {
                materails.Add(renderer.sharedMaterial);
                meshes.Add(renderer.sharedMesh);
            }
            AddComponentObject(entity, new MaterialMeshArrayBaking
            {
                Materials = materails.ToArray(),
                Meshes = meshes.ToArray()
            });
        }
    }
}