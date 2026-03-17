using Unity.Entities;
using Unity.Rendering;
using Unity.Scenes;
using Unity.Scenes.Editor;

namespace Graphix.Editor
{
    [WorldSystemFilter(WorldSystemFilterFlags.Editor)]
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    [UpdateBefore(typeof(EntitiesGraphicsSystem))]
    public partial struct SceneViewShowsRuntimeBridge : ISystem
    {
        public void OnUpdate(ref SystemState state)
        {
            EntitiesGraphicsSystem.SceneViewShowsRuntime = LiveConversionEditorSettings.LiveConversionMode == LiveConversionMode.SceneViewShowsRuntime;
        }
    }
}
