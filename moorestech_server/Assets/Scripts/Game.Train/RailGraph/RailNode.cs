using Game.Train.Blocks;
using System.Collections.Generic;
/// <summary>
/// 距離はint型で表現している。理由はNotion参照
/// </summary>

namespace Game.Train.RailGraph
{ 
    public class RailNode
    {
        public RailNodeId NodeId { get; }  // ノードを識別するためのユニークなID
        public Dictionary<RailNode, int> ConnectedNodes { get; }  // このノードからつながるノードとその距離

        public StationComponent Station { get; }  // 駅であれば駅のコンポーネント、なければnull

        public RailNode(StationComponent station = null)
        {
            NodeId = RailNodeId.Create();
            Station = station;
            ConnectedNodes = new Dictionary<RailNode, int>();
        }

        public void ConnectNode(RailNode targetNode, int distance)
        {
            if (!ConnectedNodes.ContainsKey(targetNode))
            {
                ConnectedNodes[targetNode] = distance;
            }
        }

        public override string ToString()
        {
            return $"RailNode {{ NodeId: {NodeId}, IsStation: {Station != null}, Connections: {ConnectedNodes.Count} }}";
        }
    }

}