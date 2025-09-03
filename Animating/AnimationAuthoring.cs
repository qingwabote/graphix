using System;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace Graphix
{
    public struct ChannelTarget : IBufferElementData, IEnableableComponent
    {
        public Entity Value;
    }

    public struct ClipBinging : IBufferElementData
    {
        public BlobAssetReference<Clip> Blob;
        public float Duration;
        public int TargetIndex;
        public int Outputs;
    }

    class AnimationAuthoring : MonoBehaviour
    {
        public AnimationClip[] Clips;
        public int ClipIndex;
    }

    public struct AnimationState : IComponentData
    {
        public int ClipIndex;
        public float Time;
    }

    class AnimationBaker : Baker<AnimationAuthoring>
    {
        public override void Bake(AnimationAuthoring authoring)
        {
            if (authoring.Clips == null)
            {
                return;
            }

            foreach (var clip in authoring.Clips)
            {
                if (clip == null)
                {
                    return;
                }
            }

            var entity = GetEntity(TransformUsageFlags.Dynamic);
            var channelTargets = AddBuffer<ChannelTarget>(entity);
            var clipBindings = AddBuffer<ClipBinging>(entity);
            foreach (var clip in authoring.Clips)
            {
                ref BlobArray<Channel> channels = ref clip.Blob.Value.Channels;
                float duration = 0;
                int outputs = 0;
                for (int i = 0; i < channels.Length; i++)
                {
                    ref var channel = ref channels[i];
                    duration = math.max(duration, channel.Input[^1]);
                    switch (channel.Path)
                    {
                        case ChannelPath.TRANSLATION:
                            outputs += 3;
                            break;
                        case ChannelPath.ROTATION:
                            outputs += 4;
                            break;
                        case ChannelPath.SCALE:
                            outputs += 3;
                            break;
                        default:
                            throw new Exception($"unsupported path: {channel.Path}");
                    }
                }

                clipBindings.Add(new ClipBinging
                {
                    Blob = clip.Blob,
                    Duration = duration,
                    TargetIndex = channelTargets.Length,
                    Outputs = outputs
                });

                foreach (var node in clip.Nodes)
                {
                    var target = authoring.transform;
                    foreach (var name in node.Split("/"))
                    {
                        for (int i = 0; i < target.childCount; i++)
                        {
                            var child = target.GetChild(i);
                            if (child.name == name)
                            {
                                target = child;
                                break;
                            }
                        }
                    }
                    channelTargets.Add(new ChannelTarget
                    {
                        Value = GetEntity(target, TransformUsageFlags.Dynamic)
                    });
                }
            }

            AddComponent(entity, new AnimationState
            {
                ClipIndex = authoring.ClipIndex
            });
        }
    }
}