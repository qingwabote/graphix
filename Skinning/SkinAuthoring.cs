using System;
using System.Collections.Generic;
using Bastard;
using Unity.Entities;
using Unity.Rendering;
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
        public int Joint;
    }

    public struct SkinInfo : IComponentData
    {
        public int Skin;
        public bool Baking;
        public BlobAssetReference<JointMeta> JointMeta;
        public int Joint;
    }

    public struct SkinNode : IBufferElementData
    {
        public Entity Target;
        public int Parent;
    }

    public struct JointSource : IComponentData
    {
        public long Value;
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

            var transforms = new List<Transform>();
            {
                var parent = authoring.transform;

                // assume Joints[0] is root joint
                var root = authoring.Skin.Joints[0];
                var path = root.Split("/");
                // add parents of root joint if any
                for (int i = 0; i < path.Length - 1; i++)
                {
                    var name = path[i];
                    var err = true;
                    for (int j = 0; j < parent.childCount; j++)
                    {
                        var child = parent.GetChild(j);
                        if (child.name == name)
                        {
                            transforms.Add(child);
                            parent = child;
                            err = false;
                            break;
                        }
                    }
                    if (err)
                    {
                        throw new Exception($"{name} not exists");
                    }
                }
            }
            var JointStart = transforms.Count;
            foreach (var path in authoring.Skin.Joints)
            {
                var target = authoring.transform.GetChildByPath(path);
                if (!target)
                {
                    throw new Exception($"{path} not exists");
                }
                transforms.Add(target);
            }

            var nodes = AddBuffer<SkinNode>(entity);
            for (int i = 0; i < transforms.Count; i++)
            {
                var child = transforms[i];
                var parent = i - 1;
                for (; parent > -1; parent--)
                {
                    if (transforms[parent] == child.parent)
                    {
                        break;
                    }
                }
                nodes.Add(new SkinNode
                {
                    Target = GetEntity(child, TransformUsageFlags.Dynamic),
                    Parent = parent
                });
            }
            AddComponentObject(entity, new SkinInfoBaking
            {
                Skin = authoring.Skin,
                Baking = authoring.Baking,
                Joint = JointStart,
            });
            AddComponent<JointSource>(entity);
            AddComponent<JointOffset>(entity);

            // "The transform of the node that the skin is attached to is ignored", we use a single entity to hold all MaterialMeshes. https://github.khronos.org/glTF-Tutorials/gltfTutorial/gltfTutorial_020_Skins.html
            var materails = new List<Material>();
            var meshes = new List<Mesh>();
            var renderers = GetComponentsInChildren<SkinnedMeshRenderer>();
            foreach (var renderer in renderers)
            {
                materails.Add(renderer.GetComponent<SkinnedMeshRendererAuthoring>().Material);
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