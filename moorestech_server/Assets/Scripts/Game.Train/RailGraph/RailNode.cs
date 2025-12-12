using Game.Block.Interface;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
/// <summary>
/// 距離はint型で表現している。理由はNotion参照
/// </summary>

namespace Game.Train.RailGraph
{
    public class RailNode
    {
        //Node（このクラスのインスタンス）とIdの違いに注意。また、このクラスではIdは一切使わない
        //public RailNodeId NodeId { get; }  // ノードを識別するためのユニークなID→一旦廃止。RailGraphだけが使うためのNodeIdは存在する
        // 自分に対応する裏表のノード
        //public RailNode OppositeNode { get; private set; }
        public RailControlPoint FrontControlPoint { get; private set; }
        public RailControlPoint BackControlPoint { get; private set; }
        //このノードが駅に対応するときの駅ブロックのworld座標などを格納
        public StationReference StationRef { get; set; }
        public Guid Guid { get; }

        public RailNode OppositeNode
        {
            get
            {
                return RailGraphDatastore.GetOppositeNode(this);
            }
        }

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
                //RailNodeの入力に対しつながっているRailNodeをリスト<Node>で返す
                return RailGraphDatastore.GetConnectedNodesWithDistance(this)
                    .Select(x => x.Item1);
            }
        }
        public IEnumerable<(RailNode, int)> ConnectedNodesWithDistance
        {
            get
            {
                //RailNodeの入力に対しつながっているRailNodeをリスト<Node,距離int>で返す
                return RailGraphDatastore.GetConnectedNodesWithDistance(this); 
            }
        }

        // 基本的にrailComponent以外からの呼び出しに対応。テストなど。表裏に対応しないrailnodeを作成するため
        public RailNode()
        {
            Guid = Guid.NewGuid();
            FrontControlPoint = null;
            BackControlPoint = null;
            StationRef = new StationReference();
        }
        // 表裏セットでRailGraphに登録する、基本的にrailComponent側からのみよびだす
        public static (RailNode front, RailNode back) CreatePair()
        {
            var a = new RailNode();
            var b = new RailNode();
            RailGraphDatastore.AddNodePair(a, b);
            return (a, b);
        }
        //テスト用など　、片方だけRailGraphに登録したいときに使う
        public static RailNode CreateSingleAndRegister()
        {
            var n = new RailNode();
            RailGraphDatastore.AddNode(n);
            return n;
        }


        public void SetRailControlPoints(RailControlPoint frontControlPoint, RailControlPoint backControlPoint)
        {
            FrontControlPoint = frontControlPoint;
            BackControlPoint = backControlPoint;
        }

        //RailGraphに登録する
        //基本的にrailComponent側からのみよびだす
        public void ConnectNode(RailNode targetNode, int distance)
        {
            RailGraphDatastore.ConnectNode(this, targetNode, distance);
        }
        public void DisconnectNode(RailNode targetNode)
        {
            RailGraphDatastore.DisconnectNode(this, targetNode);
        }
        //自分から入力nodeまでの距離を返す
        //UseFindPath=falseのとき
        //隣接しているNodeのみを考慮。距離を返すか見つからなければ-1
        //UseFindPath=trueのとき
        //経路探索して接続していれば距離を返す、見つからなければ-1
        public int GetDistanceToNode(RailNode node,bool UseFindPath = false)
        {
            //見つからなければ-1
            if (UseFindPath == false)
            {
                return RailGraphDatastore.GetDistanceBetweenNodes(this, node);
            }
            else 
            {
                //経路探索ありver
                var nodelist = RailGraphDatastore.FindShortestPath(this, node);
                if (nodelist == null || nodelist.Count < 2)
                {
                    return -1;
                }
                else
                {
                    //最初のノードは自分なので、次のノードまでの距離を返す、ループ
                    int totalDistance = 0;
                    for (int i = 0; i < nodelist.Count - 1; i++)
                    {
                        totalDistance += RailGraphDatastore.GetDistanceBetweenNodes(nodelist[i], nodelist[i + 1]);
                    }
                    return totalDistance;
                }
            }
        }


        //RailGraphから削除する
        public void Destroy()
        {
            RailGraphDatastore.RemoveNode(this);
            StationRef = null;
        }

    }

}
