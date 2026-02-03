using System;
using System.Collections.Generic;
using Game.Train.RailCalc;
using Game.Train.SaveLoad;
using UnityEditor;
using UnityEngine;
/// <summary>
/// 距離はint型で表現している。理由はNotion参照
/// </summary>

namespace Game.Train.RailGraph
{
    public class RailNode : IRailNode
    {
        private readonly IRailGraphDatastore _graphDatastore;
        public RailControlPoint FrontControlPoint { get; private set; }
        public RailControlPoint BackControlPoint { get; private set; }
        //このノードが駅に対応するときの駅ブロックのworld座標などを格納
        public StationReference StationRef { get; set; }
        public ConnectionDestination ConnectionDestination { get; private set; } = ConnectionDestination.Default;
        public bool HasConnectionDestination => !ConnectionDestination.IsDefault();
        public Guid Guid { get; }
        Guid IRailNode.NodeGuid => Guid;
        public IRailGraphProvider GraphProvider => _graphDatastore;
        public int NodeId => _graphDatastore.TryGetRailNodeId(this, out var nodeId) ? nodeId : -1;
        public int OppositeNodeId => NodeId ^ 1;

        // 自分に対応する裏表のノード
        public IRailNode OppositeNode
        {
            get
            {
                return _graphDatastore.TryGetRailNode(this.OppositeNodeId, out var nodeId) ? nodeId : null;
            }
        }
        public RailNode OppositeRailNode
        {
            get
            {
                return _graphDatastore.TryGetRailNode(this.OppositeNodeId, out var nodeId) ? nodeId : null;
            }
        }

        public IEnumerable<IRailNode> ConnectedNodes
        {
            get
            {
                //RailNodeの入力に対しつながっているRailNodeを列挙で返す
                foreach (var (node, _) in _graphDatastore.GetConnectedNodesWithDistance(this))
                    yield return node;
            }
        }
        public IEnumerable<(IRailNode, int)> ConnectedNodesWithDistance
        {
            get
            {
                //RailNodeの入力に対しつながっているRailNodeと距離を列挙で返す
                foreach (var entry in _graphDatastore.GetConnectedNodesWithDistance(this))
                    yield return entry;
            }
        }

        // 基本的にrailComponentからの呼び出しに対応
        public RailNode(IRailGraphDatastore graphDatastore)
        {
            // グラフプロバイダを保持する
            // Keep the graph provider dependency
            _graphDatastore = graphDatastore;
            Guid = Guid.NewGuid();
            FrontControlPoint = new RailControlPoint(new Vector3(-1, -1, -1), new Vector3(-1, -1, -1));
            BackControlPoint = new RailControlPoint(new Vector3(-1, -1, -1), new Vector3(-1, -1, -1));
            StationRef = new StationReference();
            ConnectionDestination = ConnectionDestination.Default;
        }
        // 表裏セットでRailGraphに登録する、テスト用
        public static (RailNode front, RailNode back) CreatePairAndRegister(IRailGraphDatastore graphDatastore)
        {
            var a = new RailNode(graphDatastore);
            var b = new RailNode(graphDatastore);
            graphDatastore.AddNodePair(a, b);
            return (a, b);
        }
        //テスト用など　、片方だけRailGraphに登録したいときに使う
        public static RailNode CreateSingleAndRegister(IRailGraphDatastore graphDatastore)
        {
            var n = new RailNode(graphDatastore);
            graphDatastore.AddNodeSingle(n);
            return n;
        }

        public void SetRailControlPoints(RailControlPoint frontControlPoint, RailControlPoint backControlPoint)
        {
            FrontControlPoint = frontControlPoint;
            BackControlPoint = backControlPoint;
        }

        public void SetConnectionDestination(ConnectionDestination destination)
        {
            ConnectionDestination = destination;
        }

        //RailGraphに登録する
        //デフォルトでdistanceを強制的に決めると自動距離計算はあきらめ、レール実体は描画不可能フラグがたつ
        public void ConnectNode(RailNode targetNode, int distance=-1)
        {
            ConnectNode(targetNode, Guid.Empty, distance);
        }
        public void ConnectNode(RailNode targetNode, Guid railTypeGuid, int distance=-1)
        {
            var isDrawable = distance == -1;
            if (distance == -1)
            {
                float rawLength = BezierUtility.GetBezierCurveLength(this, targetNode);
                float scaledLength = rawLength * BezierUtility.RAIL_LENGTH_SCALE;
                distance = (int)(scaledLength + 0.5f);   
            }
            _graphDatastore.ConnectNode(this, targetNode, distance, railTypeGuid, isDrawable);
        }
        public void DisconnectNode(RailNode targetNode)
        {
            _graphDatastore.DisconnectNode(this, targetNode);
        }
        //自分から入力nodeまでの距離を返す
        //UseFindPath=falseのとき
        //隣接しているNodeのみを考慮。距離を返すか見つからなければ-1
        //UseFindPath=trueのとき
        //経路探索して接続していれば距離を返す、見つからなければ-1
        public int GetDistanceToNode(IRailNode node, bool UseFindPath = false)
        {
            // 指定プロバイダで距離を計算する
            // Calculate distance via the assigned provider
            return _graphDatastore.GetDistance(this, node, UseFindPath);
        }

        //RailGraphから削除する
        public void Destroy()
        {
            _graphDatastore.RemoveNode(this);
            StationRef = null;
        }

    }

}


