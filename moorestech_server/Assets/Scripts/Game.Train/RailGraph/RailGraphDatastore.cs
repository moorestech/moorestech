using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Game.Train.RailGraph
{
    public class RailGraphDatastore
    {
        // ----- シングルトン実装用 -----
        private static RailGraphDatastore _instance;

        /// <summary>
        /// インスタンスを取得する。nullの場合は内部で生成する例。
        /// 必要に応じて明示的Initialize()呼び出しでもOK。
        /// </summary>
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

        // ----- 既存の静的コレクションは非静的に変更する -----
        private Dictionary<RailNode, int> railIdDic;
        private List<RailNode> railNodes;
        private MinHeap<int> nextidQueue;//railNodeには1つの固有のintのidを割り当てている。これはダイクストラ高速化のため。そのidをなるべく若い順に使いたい
        private List<List<(int, int)>> connectNodes;

        // コンストラクタは private にして外部から new を禁止
        private RailGraphDatastore()
        {
            railIdDic = new Dictionary<RailNode, int>();
            railNodes = new List<RailNode>();
            nextidQueue = new MinHeap<int>();
            connectNodes = new List<List<(int, int)>>();
        }

        //======================================================
        //  Public static API (外部から呼ばれるメソッド)  
        //======================================================

        public static void AddNode(RailNode node)
        {
            Instance.AddNodeInternal(node);
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

        public static int GetDistanceBetweenNodes(RailNode start, RailNode target)
        {
            return Instance.GetDistanceBetweenNodesInternal(start, target);
        }

        /// <summary>
        /// ダイクストラ法による最短経路を返す
        /// </summary>
        public static List<RailNode> FindShortestPath(RailNode startNode, RailNode targetNode)
        {
            return FindShortestPath(Instance.railIdDic[startNode], Instance.railIdDic[targetNode]);
        }

        public static List<RailNode> FindShortestPath(int startid, int targetid)
        {
            return Instance.FindShortestPathInternal(startid, targetid);
        }

        //======================================================
        //  内部実装部 (インスタンスメソッド)
        //======================================================

        private void AddNodeInternal(RailNode node)
        {
            if (railIdDic.ContainsKey(node))
                return;

            int nextid;
            if (nextidQueue.IsEmpty || (railNodes.Count < nextidQueue.Peek()))
                nextidQueue.Insert(railNodes.Count);

            nextid = nextidQueue.RemoveMin();
            if (nextid == railNodes.Count)
            {
                railNodes.Add(node);
                connectNodes.Add(new List<(int, int)>());
            }
            else
            {
                railNodes[nextid] = node;
            }
            railIdDic[node] = nextid;
        }

        private void ConnectNodeInternal(RailNode node, RailNode targetNode, int distance)
        {
            if (!railIdDic.ContainsKey(node))
                AddNodeInternal(node);
            var nodeid = railIdDic[node];
            if (!railIdDic.ContainsKey(targetNode))
                AddNodeInternal(targetNode);
            var targetid = railIdDic[targetNode];
            if (!connectNodes[nodeid].Any(x => x.Item1 == targetid))
                connectNodes[nodeid].Add((targetid, distance));
        }

        private void DisconnectNodeInternal(RailNode node, RailNode targetNode)
        {
            var nodeid = railIdDic[node];
            var targetid = railIdDic[targetNode];
            connectNodes[nodeid].RemoveAll(x => x.Item1 == targetid);
        }

        private void RemoveNodeInternal(RailNode node)
        {
            if (!railIdDic.ContainsKey(node))
                return;
            var nodeid = railIdDic[node];
            railIdDic.Remove(node);
            railNodes[nodeid] = null;
            nextidQueue.Insert(nodeid);
            connectNodes[nodeid].Clear();
            RemoveNodeTo(nodeid);
        }

        private void RemoveNodeTo(int nodeid)
        {
            for (int i = 0; i < connectNodes.Count; i++)
            {
                connectNodes[i].RemoveAll(x => x.Item1 == nodeid);
            }
        }

        private List<(RailNode, int)> GetConnectedNodesWithDistanceInternal(RailNode node)
        {
            if (!railIdDic.ContainsKey(node))
                return new List<(RailNode, int)>();
            int nodeId = railIdDic[node];
            return connectNodes[nodeId].Select(x => (railNodes[x.Item1], x.Item2)).ToList();
        }

        private int GetDistanceBetweenNodesInternal(RailNode start, RailNode target)
        {
            if (!railIdDic.ContainsKey(start) || !railIdDic.ContainsKey(target))
            {
                Debug.LogWarning("RailNodeが登録されていません");
                return -1;
            }
            int startid = railIdDic[start];
            int targetid = railIdDic[target];
            foreach (var (neighbor, distance) in connectNodes[startid])
            {
                if (neighbor == targetid)
                    return distance;
            }
            Debug.LogWarning("RailNodeがつながっていません " + startid + " to " + targetid);
            return -1;
        }

        // ダイクストラ
        private List<RailNode> FindShortestPathInternal(int startid, int targetid)
        {
            var priorityQueue = new PriorityQueue<int, int>();
            var distances = new List<int>();
            var previousNodes = new List<int>();

            for (int i = 0; i < railNodes.Count; i++)
                distances.Add(int.MaxValue);
            for (int i = 0; i < railNodes.Count; i++)
                previousNodes.Add(-1);

            distances[startid] = 0;
            priorityQueue.Enqueue(startid, 0);

            while (priorityQueue.Count > 0)
            {
                var currentNodecnt = priorityQueue.Dequeue();
                if (currentNodecnt == targetid)
                    break;

                foreach (var (neighbor, distance) in connectNodes[currentNodecnt])
                {
                    int newDistance = distances[currentNodecnt] + distance;
                    if (newDistance < 0)
                        continue;
                    if (newDistance < distances[neighbor])
                    {
                        distances[neighbor] = newDistance;
                        previousNodes[neighbor] = currentNodecnt;
                        priorityQueue.Enqueue(neighbor, newDistance);
                    }
                }
            }

            var path = new List<int>();
            var current = targetid;
            while (current != -1)
            {
                path.Add(current);
                current = previousNodes[current];
            }

            if (path.Last() != startid)
            {
                return new List<RailNode>();
            }
            path.Reverse();
            var pathNodes = path.Select(id => railNodes[id]).ToList();
            return pathNodes;
        }
    }
}
