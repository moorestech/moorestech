using Game.Train.Blocks;
using System.Collections.Generic;
/// <summary>
/// 距離はint型で表現している。理由はNotion参照
/// </summary>

namespace Game.Train.RailGraph
{   
    public class RailNode
    {
        //public RailNodeId NodeId { get; }  // ノードを識別するためのユニークなID→一旦廃止。RailGraphだけが使うためのNodeIdは存在する
        //Node（このクラスのインスタンス）とIdの違いに注意。また、このクラスではIdは一切使わない
        public List<(RailNode, int)> ConnectedNodes { get; }  // つながる先のノードとその距離
        public StationComponent Station { get; }  // 駅であれば駅のコンポーネント、なければnull
        private readonly RailGraphDatastore _railGraph; // Graph への参照


        public RailNode(RailGraphDatastore railGraph, StationComponent station = null)
        {
            _railGraph = railGraph;
            Station = station;
        }

        /*
        public List<(RailNode targetNode, int distance)> GetConnections()
        {
            return _railGraph.GetConnections(this);
        }
        */
        

        public void ConnectNode(RailNode targetNode, int distance)
        {
            _railGraph.ConnectNode(this, targetNode, distance);
        }

    }

}