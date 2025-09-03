using System.Collections.Generic;
using Unity.Entities;
using UnityEngine;

namespace Graphix
{
    public partial struct Renderer : ISystem
    {
        private static List<MaterialMeshArray> s_MaterialMeshArrays = new(2);

        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<MaterialMeshArray>();
        }

        public void OnUpdate(ref SystemState state)
        {
            s_MaterialMeshArrays.Clear();
            state.EntityManager.GetAllUniqueSharedComponentsManaged(s_MaterialMeshArrays);
            Batch.Render(s_MaterialMeshArrays[1].Materials, s_MaterialMeshArrays[1].Meshes);
        }
    }
}