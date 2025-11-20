using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Unity.Entities;
using Unity.Collections;
using System.Linq;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;
using System;
using Bastard;

namespace Graphix
{
    public class Postprocessor : AssetPostprocessor
    {
        struct CurveGroup
        {
            public string Node;
            public ChannelPath Path;
            public AnimationCurve[] Components;

            public int KeyCount => Components[0].length;

            public void GetKeyTimes(ref BlobBuilderArray<float> input)
            {
                for (int i = 0; i < Components[0].length; i++)
                {
                    input[i] = Components[0][i].time;
                }
            }
        }

        void OnPostprocessAnimation(GameObject root, UnityEngine.AnimationClip goClip)
        {
            List<CurveGroup> curveGroups = new();

            var bindings = AnimationUtility.GetCurveBindings(goClip);
            int index = 0;
            while (index < bindings.Length)
            {
                var binding0 = bindings[index];
                if (binding0.propertyName == "m_LocalRotation.x")
                {
                    AnimationCurve curve0 = AnimationUtility.GetEditorCurve(goClip, binding0);
                    var binding1 = bindings[index + 1];
                    Debug.Assert(binding1.propertyName == "m_LocalRotation.y");
                    AnimationCurve curve1 = AnimationUtility.GetEditorCurve(goClip, binding1);
                    var binding2 = bindings[index + 2];
                    Debug.Assert(binding2.propertyName == "m_LocalRotation.z");
                    AnimationCurve curve2 = AnimationUtility.GetEditorCurve(goClip, binding2);
                    var binding3 = bindings[index + 3];
                    Debug.Assert(binding3.propertyName == "m_LocalRotation.w");
                    AnimationCurve curve3 = AnimationUtility.GetEditorCurve(goClip, binding3);

                    var path = binding0.path;
                    Debug.Assert(binding1.path == path && binding2.path == path && binding3.path == path);

                    var length = curve0.keys.Length;
                    Debug.Assert(curve1.keys.Length == length && curve2.keys.Length == length && curve3.keys.Length == length);

                    for (int i = 0; i < length; i++)
                    {
                        var time = curve0.keys[i].time;
                        Debug.Assert(curve1.keys[i].time == time && curve2.keys[i].time == time && curve3.keys[i].time == time);
                    }

                    curveGroups.Add(new CurveGroup
                    {
                        Node = path,
                        Path = ChannelPath.ROTATION,
                        Components = new AnimationCurve[] { curve0, curve1, curve2, curve3 }
                    });

                    index += 4;
                }
                else if (binding0.propertyName == "m_LocalPosition.x")
                {
                    AnimationCurve curve0 = AnimationUtility.GetEditorCurve(goClip, binding0);
                    var binding1 = bindings[index + 1];
                    Debug.Assert(binding1.propertyName == "m_LocalPosition.y");
                    AnimationCurve curve1 = AnimationUtility.GetEditorCurve(goClip, binding1);
                    var binding2 = bindings[index + 2];
                    Debug.Assert(binding2.propertyName == "m_LocalPosition.z");
                    AnimationCurve curve2 = AnimationUtility.GetEditorCurve(goClip, binding2);

                    var path = binding0.path;
                    Debug.Assert(binding1.path == path && binding2.path == path);

                    var length = curve0.keys.Length;
                    Debug.Assert(curve1.keys.Length == length && curve2.keys.Length == length);

                    for (int i = 0; i < length; i++)
                    {
                        var time = curve0.keys[i].time;
                        Debug.Assert(curve1.keys[i].time == time && curve2.keys[i].time == time);
                    }

                    curveGroups.Add(new CurveGroup
                    {
                        Node = path,
                        Path = ChannelPath.TRANSLATION,
                        Components = new AnimationCurve[] { curve0, curve1, curve2 }
                    });

                    index += 3;
                }
                else if (binding0.propertyName == "m_LocalScale.x")
                {
                    AnimationCurve curve0 = AnimationUtility.GetEditorCurve(goClip, binding0);
                    var binding1 = bindings[index + 1];
                    Debug.Assert(binding1.propertyName == "m_LocalScale.y");
                    AnimationCurve curve1 = AnimationUtility.GetEditorCurve(goClip, binding1);
                    var binding2 = bindings[index + 2];
                    Debug.Assert(binding2.propertyName == "m_LocalScale.z");
                    AnimationCurve curve2 = AnimationUtility.GetEditorCurve(goClip, binding2);

                    var path = binding0.path;
                    Debug.Assert(binding1.path == path && binding2.path == path);

                    var length = curve0.keys.Length;
                    Debug.Assert(curve1.keys.Length == length && curve2.keys.Length == length);

                    for (int i = 0; i < length; i++)
                    {
                        var time = curve0.keys[i].time;
                        Debug.Assert(curve1.keys[i].time == time && curve2.keys[i].time == time);
                    }

                    curveGroups.Add(new CurveGroup
                    {
                        Node = path,
                        Path = ChannelPath.SCALE,
                        Components = new AnimationCurve[] { curve0, curve1, curve2 }
                    });

                    index += 3;
                }
                else
                {
                    var parts = binding0.propertyName.Split('.');
                    var extraNames = new[] { "MotionT", "MotionQ", "RootT", "RootQ", "LeftFootT", "LeftFootQ", "RightFootT", "RightFootQ", "LeftHandT", "LeftHandQ", "RightHandT", "RightHandQ" };
                    if (extraNames.Contains(parts[0]))
                    {
                        index++;
                    }
                    else
                    {
                        Debug.LogWarning($"Unsupported property {binding0.propertyName}");
                        index++;
                    }
                }
            }

            var builder = new BlobBuilder(Allocator.Temp);
            ref Clip clip = ref builder.ConstructRoot<Clip>();
            BlobBuilderArray<Channel> channels = builder.Allocate(ref clip.Channels, curveGroups.Count);
            var nodes = new string[curveGroups.Count];
            for (int groupIndex = 0; groupIndex < curveGroups.Count; groupIndex++)
            {
                var group = curveGroups[groupIndex];
                ref var channel = ref channels[groupIndex];
                BlobBuilderArray<float> input = builder.Allocate(ref channel.Input, group.KeyCount);
                group.GetKeyTimes(ref input);
                var components = group.Components.Length;
                BlobBuilderArray<float> output = builder.Allocate(ref channel.Output, group.KeyCount * components);
                for (int keyIndex = 0; keyIndex < group.KeyCount; keyIndex++)
                {
                    for (int i = 0; i < components; i++)
                    {
                        output[keyIndex * components + i] = group.Components[i][keyIndex].value;
                    }
                }
                channel.Path = group.Path;

                nodes[groupIndex] = group.Node;
            }
            clip.Duration = goClip.length;

            var animationClip = ScriptableObject.CreateInstance<AnimationClip>();
            animationClip.name = goClip.name;
            animationClip.Nodes = nodes;
            animationClip.Blob = builder.CreateBlobAssetReference<Clip>(Allocator.Persistent);
            context.AddObjectToAsset($"Graphix_{animationClip.name}", animationClip);
        }

        void OnPostprocessModel(GameObject gameObject)
        {
            var skinnedRenderer = gameObject.GetComponentInChildren<UnityEngine.SkinnedMeshRenderer>();
            if (skinnedRenderer)
            {
                var skin = ScriptableObject.CreateInstance<Skin>();
                skin.name = "Skin_0";

                List<(string Path, int Location)> nodes = new();
                {
                    var joints = new (string Path, int Location)[skinnedRenderer.bones.Length];
                    for (int i = 0; i < joints.Length; i++)
                    {
                        joints[i].Path = skinnedRenderer.bones[i].transform.RelativePath(gameObject.transform);
                        joints[i].Location = i;
                    }
                    Array.Sort(joints, (a, b) =>
                    {
                        return string.Compare(a.Path, b.Path, StringComparison.Ordinal);
                    });
                    foreach (var joint in joints)
                    {
                        var path = joint.Path;
                        List<string> parents = new();
                        while (true)
                        {
                            int index = path.LastIndexOf('/');
                            if (index < 0)
                            {
                                break;
                            }
                            var parent = path.Substring(0, index);
                            int i = nodes.Count - 1;
                            for (; i > -1; i--)
                            {
                                if (nodes[i].Path == parent)
                                {
                                    break;
                                }
                            }
                            if (i > -1)
                            {
                                break;
                            }
                            parents.Add(parent);
                            path = parent;
                        }
                        for (int i = parents.Count - 1; i > -1; i--)
                        {
                            nodes.Add(new(parents[i], -1));
                        }
                        nodes.Add(joint);
                    }
                    skin.Nodes = nodes.Select(x => x.Path).ToArray();
                }

                var bindposes = skinnedRenderer.sharedMesh.GetBindposes();

                var builder = new BlobBuilder(Allocator.Temp);
                ref var meta = ref builder.ConstructRoot<JointMeta>();
                var inverseBindMatrices = builder.Allocate(ref meta.InverseBindMatrices, bindposes.Length);
                unsafe
                {
                    UnsafeUtility.MemCpy(inverseBindMatrices.GetUnsafePtr(), bindposes.GetUnsafeReadOnlyPtr(), UnsafeUtility.SizeOf<float4x4>() * inverseBindMatrices.Length);
                }
                var Locations = builder.Allocate(ref meta.Locations, nodes.Count);
                for (int i = 0; i < Locations.Length; i++)
                {
                    Locations[i] = nodes[i].Location;
                }
                skin.JointMeta = builder.CreateBlobAssetReference<JointMeta>(Allocator.Persistent);


                var skinAuthoring = gameObject.AddComponent<SkinAuthoring>();
                skinAuthoring.Skin = skin;

                var materials = new Dictionary<Material, Material>();
                var skinnedRenderers = gameObject.GetComponentsInChildren<SkinnedMeshRenderer>();
                foreach (var renderer in skinnedRenderers)
                {
                    if (!materials.TryGetValue(renderer.sharedMaterial, out Material material))
                    {
                        material = new Material(renderer.sharedMaterial)
                        {
                            shader = Shader.Find("Graphix/Phong"),
                            enableInstancing = true
                        };
                        material.SetFloat("_SKINNING", 1);
                        materials.Add(renderer.sharedMaterial, material);

                        context.AddObjectToAsset($"Graphix_{material.name}", material);
                    }
                    var authoring = renderer.gameObject.AddComponent<SkinnedMeshRendererAuthoring>();
                    authoring.Material = material;
                }
                context.AddObjectToAsset($"Graphix_{skin.name}", skin);
            }
        }
    }
}