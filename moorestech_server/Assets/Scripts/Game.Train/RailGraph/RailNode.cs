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
        public List<(RailNode, int)> ConnectedNodes { get; }  // つながる先のノードとその距離

        public StationComponent Station { get; }  // 駅であれば駅のコンポーネント、なければnull

        public RailNode(StationComponent station = null)
        {
            NodeId = RailNodeId.Create();
            Station = station;
            ConnectedNodes = new List<(RailNode, int)>();
        }

        public void ConnectNode(RailNode targetNode, int distance)
        {
            ConnectedNodes.Add((targetNode, distance));
            //TODO、ここで分岐のつながる先のノードの位置を見て、左から順番になるように並び替えを行う
        }

        public override string ToString()
        {
            return $"RailNode {{ NodeId: {NodeId}, IsStation: {Station != null}, Connections: {ConnectedNodes.Count} }}";
        }
    }

}