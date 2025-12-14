using Game.Train.Common;
using System;
using System.Collections.Generic;
using System.Linq;
using UniRx;
using UnityEngine;

namespace Game.Train.RailGraph
{
    public class RailGraphDatastore
    {
        // ----- シングルトン実装用 -----
        private static RailGraphDatastore _instance;

        private static RailGraphDatastore Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = new RailGraphDatastore();
                }
                return _instance;
            }
        }

        //①railnodeとrailnodeidの対応関係を記憶する辞書。id化はダイクストラ法高速化のため(約2倍以上)
        private Dictionary<RailNode, int> railNodeToId;//①
        private List<RailNode> railNodes;//①
        private RailNodeIdAllocator nodeIdAllocator;//① railNodeには1つの固有のintのidを割り当てている。これはダイクストラ高速化のため。そのidをなるべく若い順に使いたい。具体的な処理はRailNodeIdAllocatorコードのコメントをみて
        //②railnode同士の接続情報を記憶するリスト。connectNodes[railnodeid]がそのrailnodeからつながっているrailnodeidと距離のリストになる
        private List<List<(int, int)>> connectNodes;
        private RailGraphPathFinder _pathFinder;//ダイクストラ法は専用クラスに委譲する
        //③railnodeとConnectionDestinationを1:1対応で記憶する辞書。ConnectionDestinationは座標や表裏情報をもつのでセーブ・ロード用やクライアント通信に使う
        // 座標はセーブ時と列車座標を求めるときや、RailPositionのRailNode情報から3D座標を復元するために使う
        private Dictionary<ConnectionDestination, int> connectionDestinationToRailId;//③
        //④駅のレール端座標を記録。これは駅ブロック同士を隣接させて設置したとき自動でRailComponentが接続するので、その隣接探索コストをO(N)からO(1)にするためのもの。登録するのは駅関連のだけ
        // RailComponent座標から(RailComponent+FrontBack)を引く辞書。この座標は (ブロック座標+オフセット)*回転 のVector3。ブロック座標が巨大なときの浮動小数点数誤差は目を瞑る
        // 例：駅と貨物駅を向かい合わせに設置したときRailComponent1のFrontとRailComponent3のFrontが重なる→RailComponent1.connectRailComponent(RailComponent3, true, false)を呼ぶことになる
        // ここではRailComponent1のFrontやRailComponent3のFrontをConnectionDestinationであらわす(事実上、駅から離れる方向に向かうnodeだけをConnectionDestinationであらわしている)
        private Dictionary<Vector3Int, (ConnectionDestination first, ConnectionDestination second)> railPositionToConnectionDestination;
        public static Dictionary<Vector3Int, (ConnectionDestination first, ConnectionDestination second)> RailPositionToConnectionDestination => _instance.railPositionToConnectionDestination;

        // レールグラフ更新イベント
        // Rail graph update event
        private readonly RailGraphNotifier _notifier = null!;
        private readonly RailNodeInitializationNotifier _nodeInitializationNotifier = null!;
        private readonly RailConnectionInitializationNotifier _connectionInitializationNotifier = null!;

        public static IObservable<RailNodeInitializationData> RailNodeInitializedEvent => Instance._nodeInitializationNotifier.RailNodeInitializedEvent;
        public static IObservable<RailConnectionInitializationNotifier.RailConnectionInitializationData> RailConnectionInitializedEvent => Instance._connectionInitializationNotifier.RailConnectionInitializedEvent;

        // ハッシュキャッシュ制御
        // Hash cache control
        private uint _cachedGraphHash;
        private bool _isHashDirty;

        public RailGraphDatastore()
        {
            #region internal

            (bool success, RailComponentID id) TryResolveRailComponentId(RailNode node)
            {
                var destination = node.ConnectionDestination;
                if (destination.IsDefault())
                    return (false, default);
                return (true, node.ConnectionDestination.railComponentID);
            }
            #endregion

            InitializeDataStore();
            // RailNode -> RailComponentID の解決ロジックを Notifier に渡す
            _notifier = new RailGraphNotifier(TryResolveRailComponentId);
            _nodeInitializationNotifier = new RailNodeInitializationNotifier();
            _connectionInitializationNotifier = new RailConnectionInitializationNotifier();
            _instance = this;
        }

        private void InitializeDataStore()
        {
            railNodeToId = new Dictionary<RailNode, int>();
            railNodes = new List<RailNode>();
            nodeIdAllocator = new RailNodeIdAllocator(EnsureRailNodeSlot);
            connectNodes = new List<List<(int, int)>>();
            connectionDestinationToRailId = new Dictionary<ConnectionDestination, int>();
            railPositionToConnectionDestination = new Dictionary<Vector3Int, (ConnectionDestination first, ConnectionDestination second)>();
            _pathFinder = new RailGraphPathFinder();
            _cachedGraphHash = 0;
            _isHashDirty = true;
        }

        private void ResetInternalState()
        {
            //railNodes内のnode全部remove foreachでやる
            foreach (var node in railNodes.ToList())
            {
                if (node != null)
                    RemoveNode(node);
            }
            // RailGraphUpdateEvent の再生成を Notifier に委譲
            _notifier.Reset();
            _nodeInitializationNotifier.Reset();
            _connectionInitializationNotifier.Reset();
            InitializeDataStore();
        }

        public static void ResetInstance()
        {
            if (_instance == null)
            {
                _instance = new RailGraphDatastore();
                return;
            }

            _instance.ResetInternalState();
        }


        //======================================================
        //  Public static API (外部から呼ばれるメソッド)  
        //======================================================

        public static void AddNodeSingle(RailNode node)
        {
            Instance.AddNodeSingleInternal(node);
        }
        public static void AddNodePair(RailNode node1, RailNode node2) 
        {
            Instance.AddNodePairInternal(node1, node2);
        }

        public static RailNode GetOppositeNode(RailNode node) 
        {
            return Instance.GetOppositeNodeInternal(node);
        }

        public static void ConnectNode(RailNode node, RailNode targetNode, int distance)
        {
            Instance.ConnectNodeInternal(node, targetNode, distance);
        }

        public static void DisconnectNode(RailNode node, RailNode targetNode)
        {
            Instance.DisconnectNodeInternal(node, targetNode);
        }

        public static List<(RailNode, int)> GetConnectedNodesWithDistance(RailNode node)
        {
            return Instance.GetConnectedNodesWithDistanceInternal(node);
        }

        public static void RemoveNode(RailNode node)
        {
            Instance.RemoveNodeInternal(node);
        }

        public static RailNode ResolveRailNode(ConnectionDestination destination)
        {
            return Instance.ResolveRailNodeInternal(destination);
        }

        public static int GetDistanceBetweenNodes(RailNode start, RailNode target, bool logging = true)
        {
            return Instance.GetDistanceBetweenNodesInternal(start, target, logging);
        }

        /// <summary>
        /// ダイクストラ法による最短経路を返す
        /// </summary>
        public static List<RailNode> FindShortestPath(RailNode startNode, RailNode targetNode)
        {
            return FindShortestPath(Instance.railNodeToId[startNode], Instance.railNodeToId[targetNode]);
        }

        public static List<RailNode> FindShortestPath(int startid, int targetid)
        {
            return Instance.FindShortestPathInternal(startid, targetid);
        }

        public static bool TryGetRailNodeId(RailNode node, out int nodeId)
        {
            return Instance.TryGetRailNodeIdInternal(node, out nodeId);
        }

        public static bool TryGetRailNode(int nodeId, out RailNode railNode)
        {
            return Instance.TryGetRailNodeInternal(nodeId, out railNode);
        }

        // ハッシュ取得メソッド
        // Get hash method
        public static uint GetConnectNodesHash()
        {
            return Instance.GetConnectNodesHashInternal();
        }

        //======================================================
        //  内部実装部 (インスタンスメソッド)
        //======================================================

        // RailNode/接続リストの容量を不足させない。新規nodeid割当コード用
        // Ensure node and connection lists have enough slots
        private void EnsureRailNodeSlot(int nodeId)
        {
            while (railNodes.Count <= nodeId)
            {
                railNodes.Add(null);
                connectNodes.Add(new List<(int, int)>());
            }
        }

        private void AddNodeSingleInternal(RailNode node)
        {
            if (railNodeToId.ContainsKey(node))
                return;
            var nodeId = nodeIdAllocator.Rent();
            connectNodes[nodeId].Clear();
            railNodes[nodeId] = node;
            railNodeToId[node] = nodeId;
            if (!node.ConnectionDestination.IsDefault())
                connectionDestinationToRailId[node.ConnectionDestination] = nodeId;
            NotifyNodeInitialized(nodeId);
            MarkHashDirty();
        }

        private void AddNodePairInternal(RailNode node1, RailNode node2) 
        {
            if (railNodeToId.ContainsKey(node1))
                return;
            if (railNodeToId.ContainsKey(node2))
                return;
            var (nodeId1, nodeId2) = nodeIdAllocator.Rent2();
            connectNodes[nodeId1].Clear();
            connectNodes[nodeId2].Clear();
            railNodes[nodeId1] = node1;
            railNodes[nodeId2] = node2;
            railNodeToId[node1] = nodeId1;
            railNodeToId[node2] = nodeId2;

            if (!node1.ConnectionDestination.IsDefault())
                connectionDestinationToRailId[node1.ConnectionDestination] = nodeId1;
            if (!node2.ConnectionDestination.IsDefault())
                connectionDestinationToRailId[node2.ConnectionDestination] = nodeId2;

            NotifyNodeInitialized(nodeId1);
            NotifyNodeInitialized(nodeId2);
            MarkHashDirty();
        }

        private RailNode GetOppositeNodeInternal(RailNode node) 
        {
            if (!railNodeToId.ContainsKey(node))
                return null;
            var nodeid = railNodeToId[node];
            var oppositeid = nodeid ^ 1;//表裏ノードはidが1違いなのでxorで求められる
            if (oppositeid < 0 || oppositeid >= railNodes.Count)
                return null;
            return railNodes[oppositeid];
        }

        private void ConnectNodeInternal(RailNode node, RailNode targetNode, int distance)
        {
            if (!railNodeToId.ContainsKey(node))
                throw new InvalidOperationException("Attempted to connect a RailNode that is not registered in RailGraphDatastore.");
            var nodeid = railNodeToId[node];
            if (!railNodeToId.ContainsKey(targetNode))
                throw new InvalidOperationException("Attempted to connect to a RailNode that is not registered in RailGraphDatastore.");
            var targetid = railNodeToId[targetNode];
            if (!connectNodes[nodeid].Any(x => x.Item1 == targetid))
            {
                connectNodes[nodeid].Add((targetid, distance));
                // レールグラフ更新イベントを発火
                // Fire rail graph update event
                _notifier.NotifyRailGraphUpdate(node, targetNode);
                _connectionInitializationNotifier.Notify(nodeid, targetid, distance);
            }
            else//もし登録済みなら距離を上書き
            {
                connectNodes[nodeid].RemoveAll(x => x.Item1 == targetid);
                connectNodes[nodeid].Add((targetid, distance));
                // TODO 距離変更の通知は未実装
            }

            MarkHashDirty();
        }

        private void DisconnectNodeInternal(RailNode node, RailNode targetNode)
        {
            var nodeid = railNodeToId[node];
            var targetid = railNodeToId[targetNode];
            connectNodes[nodeid].RemoveAll(x => x.Item1 == targetid);
            // レールグラフ更新イベントを発火
            // TODO 削除関連はまだ未対応
            MarkHashDirty();
        }

        private void RemoveNodeInternal(RailNode node)
        {
            if (!railNodeToId.ContainsKey(node))
                return;
            TrainDiagramManager.Instance.NotifyNodeRemoval(node);
            TrainRailPositionManager.Instance.NotifyNodeRemoval(node);
            var nodeid = railNodeToId[node];
            railNodeToId.Remove(node);
            if (node.HasConnectionDestination) 
            {
                connectionDestinationToRailId.Remove(node.ConnectionDestination);
            }
            railNodes[nodeid] = null;
            nodeIdAllocator.Return(nodeid);
            connectNodes[nodeid].Clear();
            RemoveNodeTo(nodeid);
            MarkHashDirty();
        }

        private void RemoveNodeTo(int nodeid)
        {
            for (int i = 0; i < connectNodes.Count; i++)
            {
                connectNodes[i].RemoveAll(x => x.Item1 == nodeid);
            }
            MarkHashDirty();
        }

        private bool TryGetRailNodeIdInternal(RailNode node, out int nodeId)
        {
            nodeId = -1;
            if (node == null)
            {
                return false;
            }
            return railNodeToId.TryGetValue(node, out nodeId);
        }

        private bool TryGetRailNodeInternal(int nodeId, out RailNode node)
        {
            node = null;
            if (nodeId < 0 || nodeId >= railNodes.Count)
            {
                return false;
            }
            node = railNodes[nodeId];
            return node != null;
        }

        private void NotifyNodeInitialized(int nodeid)
        {
            var node = railNodes[nodeid];
            var destination = node.ConnectionDestination.IsDefault() ? ConnectionDestination.Default : node.ConnectionDestination;
            var frontControlPoint = node.FrontControlPoint.ControlPointPosition;
            var backControlPoint = node.BackControlPoint.ControlPointPosition;
            var originPoint = node.FrontControlPoint.OriginalPosition;
            _nodeInitializationNotifier.Notify(new RailNodeInitializationData(
                nodeid,
                node.Guid,
                destination,
                originPoint,
                frontControlPoint,
                backControlPoint));
        }

        private RailNode ResolveRailNodeInternal(ConnectionDestination destination)
        {
            if (destination.IsDefault())
            {
                return null;
            }
            if (!connectionDestinationToRailId.TryGetValue(destination, out var nodeId))
            {
                return null;
            }
            if ((nodeId < 0) || (nodeId >= railNodes.Count))
            {
                return null;
            }
            return railNodes[nodeId];
        }

        private List<(RailNode, int)> GetConnectedNodesWithDistanceInternal(RailNode node)
        {
            if (!railNodeToId.ContainsKey(node))
                return new List<(RailNode, int)>();
            int nodeId = railNodeToId[node];
            return connectNodes[nodeId].Select(x => (railNodes[x.Item1], x.Item2)).ToList();
        }

        private int GetDistanceBetweenNodesInternal(RailNode start, RailNode target, bool logging = true)
        {
            if (!railNodeToId.ContainsKey(start) || !railNodeToId.ContainsKey(target))
            {
                if (logging)
                    Debug.LogWarning("RailNodeが登録されていません");
                return -1;
            }
            int startid = railNodeToId[start];
            int targetid = railNodeToId[target];
            foreach (var (neighbor, distance) in connectNodes[startid])
            {
                if (neighbor == targetid)
                    return distance;
            }
            if (logging)
                Debug.LogWarning("RailNodeがつながっていません " + startid + " to " + targetid);
            return -1;
        }

        // ダイクストラ startからtargetへのnodeリストを返す、0がstart、最後がtarget
        private List<RailNode> FindShortestPathInternal(int startid, int targetid)
        {
            // RailGraphPathFinder に処理を委譲
            return _pathFinder.FindShortestPath(railNodes, connectNodes, startid, targetid);
        }

        private uint GetConnectNodesHashInternal()
        {
            if (!_isHashDirty)
                return _cachedGraphHash;
            _cachedGraphHash = RailGraphHashCalculator.ComputeConnectNodesHash(connectNodes);
            _isHashDirty = false;
            return _cachedGraphHash;
        }

        private void MarkHashDirty()
        {
            _isHashDirty = true;
        }
    }

    public readonly struct RailNodeInitializationData
    {
        public RailNodeInitializationData(int nodeId, Guid nodeGuid, ConnectionDestination connectionDestination, Vector3 originPoint, Vector3 frontControlPoint, Vector3 backControlPoint)
        {
            NodeId = nodeId;
            NodeGuid = nodeGuid;
            ConnectionDestination = connectionDestination;
            OriginPoint = originPoint;
            FrontControlPoint = frontControlPoint;
            BackControlPoint = backControlPoint;
        }

        public int NodeId { get; }
        public Guid NodeGuid { get; }
        public ConnectionDestination ConnectionDestination { get; }
        public Vector3 OriginPoint { get; }
        public Vector3 FrontControlPoint { get; }
        public Vector3 BackControlPoint { get; }
    }

    public readonly struct RailNodeRegistrationInfo
    {
        public RailNodeRegistrationInfo(ConnectionDestination connectionDestination, RailControlPoint primaryControlPoint, RailControlPoint oppositeControlPoint)
        {
            ConnectionDestination = connectionDestination;
            PrimaryControlPoint = primaryControlPoint;
            OppositeControlPoint = oppositeControlPoint;
        }

        public ConnectionDestination ConnectionDestination { get; }
        public RailControlPoint PrimaryControlPoint { get; }
        public RailControlPoint OppositeControlPoint { get; }
    }

}
