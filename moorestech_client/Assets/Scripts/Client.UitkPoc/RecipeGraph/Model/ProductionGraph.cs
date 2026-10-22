using System.Collections.Generic;

namespace Client.UitkPoc.RecipeGraph.Model
{
    // 1資源=1ノードに合算した生産ライン。辺は「資源が生産ノードから消費ノードへ毎分どれだけ流れるか」
    // Production line merged to one node per resource; edges carry per-minute flow from producer to consumer
    public class ProductionGraph
    {
        public readonly List<ProductionNode> Nodes = new();
        public readonly List<ProductionEdge> Edges = new();
    }

    public class ProductionNode
    {
        public readonly string ResourceGuid;
        public readonly string ResourceName;
        public readonly string MachineName;
        public readonly bool IsRaw;
        public readonly bool IsFluid;
        public readonly int AlternativeRecipeCount;
        public readonly float RatePerMinute;
        public readonly float MachineCount;

        public ProductionNode(string resourceGuid, string resourceName, string machineName, bool isRaw, bool isFluid, int alternativeRecipeCount, float ratePerMinute, float machineCount)
        {
            ResourceGuid = resourceGuid;
            ResourceName = resourceName;
            MachineName = machineName;
            IsRaw = isRaw;
            IsFluid = isFluid;
            AlternativeRecipeCount = alternativeRecipeCount;
            RatePerMinute = ratePerMinute;
            MachineCount = machineCount;
        }
    }

    public class ProductionEdge
    {
        public readonly int FromNode;
        public readonly int ToNode;
        public readonly string ResourceName;
        public readonly bool IsFluid;
        public float RatePerMinute { get; private set; }

        public ProductionEdge(int fromNode, int toNode, string resourceName, bool isFluid)
        {
            FromNode = fromNode;
            ToNode = toNode;
            ResourceName = resourceName;
            IsFluid = isFluid;
        }

        public void AddRate(float ratePerMinute)
        {
            RatePerMinute += ratePerMinute;
        }
    }
}
