using Game.Train.Blocks;
using System;
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


        // 駅であれば駅のコンポーネント、なければnull
        public StationComponent Station { get; private set; }
        // 自分に対応する裏表のノード
        public RailNode OppositeNode { get; private set; }
        private readonly RailGraphDatastore _railGraph; // Graph への参照

        
        /// なぜ IEnumerable を使うのか？
        //IEnumerable<RailNode> を使う理由には以下があります：
        //柔軟性:
        //  使用する側で foreach を使って簡単に列挙できる。
        //  必要に応じてリストや配列に変換可能。
        //遅延評価:
        //  コレクションが大きい場合でも、全体を一度にメモリに読み込む必要がない。
        //抽象化:
        //  呼び出し元に具体的なコレクションの型（List<T> や Array など）を意識させない。
        /// </summary>
        public IEnumerable<RailNode> ConnectedNodes
        {
            get
            {
                return _railGraph.GetConnectedNodes(this);
            }
        }


        public RailNode(RailGraphDatastore railGraph, StationComponent station = null)
        {
            _railGraph = railGraph;
            Station = station;
            railGraph.AddNode(this);
        }

        //RailNode oppositeNode のset。基本的にrailComponentのコンストラクタでのみ使う
        public void SetOppositeNode(RailNode oppositeNode)
        {
            OppositeNode = oppositeNode;
        }



        public void ConnectNode(RailNode targetNode, int distance)
        {
            _railGraph.ConnectNode(this, targetNode, distance);
        }


    }

}