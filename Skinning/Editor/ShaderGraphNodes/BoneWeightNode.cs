using UnityEngine;
using UnityEditor.Graphing;
using UnityEditor.ShaderGraph.Internal;

namespace UnityEditor.ShaderGraph
{
    [FormerName("UnityEngine.MaterialGraph.BoneWeightNode")]
    [Title("Input", "Geometry", "BoneWeight")]
    class BoneWeightNode : AbstractMaterialNode, IMayRequireVertexSkinning
    {
        public override int latestVersion => 1;
        private const int kOutputSlotId = 0;
        public const string kOutputSlotName = "Out";

        public BoneWeightNode()
        {
            name = "BoneWeight";
            precision = Precision.Single;
            UpdateNodeAfterDeserialization();
        }

        public sealed override void UpdateNodeAfterDeserialization()
        {
            AddSlot(new Vector4MaterialSlot(kOutputSlotId, kOutputSlotName, kOutputSlotName, SlotType.Output, Vector4.zero));
            RemoveSlotsNameNotMatching(new[] { kOutputSlotId });
        }

        public override string GetVariableNameForSlot(int slotId)
        {
            return "IN.BoneWeights";
        }

        public bool RequiresVertexSkinning(ShaderStageCapability stageCapability = ShaderStageCapability.All)
        {
            return true;
        }

        public override void OnAfterMultiDeserialize(string json)
        {
            base.OnAfterMultiDeserialize(json);
            if (sgVersion < 1)
                ChangeVersion(1);
        }
    }
}
