using Game.Train.Blocks;
using System.Collections.Generic;
/// <summary>
/// 距離は暫定的にint型で表現しているが、実際のゲームで問題が起きそうならLong intにしたり経路探索のときだけ浮動小数点数使う
/// </summary>

namespace Game.Train.RailGraph
{ 
    public class RailNode
    {
        public int NodeId { get; }  // ノードを識別するためのユニークなID
        public Dictionary<RailNode, int> ConnectedNodes { get; }  // このノードからつながるノードとその距離

        public StationComponent Station { get; }  // 駅であれば駅のコンポーネント、なければnull

        public RailNode(int nodeId, StationComponent station = null)
        {
            NodeId = nodeId;
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