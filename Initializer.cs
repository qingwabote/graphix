using Unity.Entities;

namespace Graphix
{
    public partial struct Initializer : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            MaterialProperty.Initialize(ref state);
        }

        public void OnUpdate(ref SystemState state)
        {

        }
    }
}