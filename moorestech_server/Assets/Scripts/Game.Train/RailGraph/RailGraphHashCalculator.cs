using System;
using System.Collections.Generic;
using ClassLibrary;
using Game.Train.SaveLoad;
using UnityEngine;

namespace Game.Train.RailGraph
{
    // レール網のハッシュ。ノードごと・接続リストごとに独立したハッシュ(識別子は nodeId)を作り、順序に依存しない加算で畳む(ElementHash。列車・ベルト・流体と同じ部品)
    // Hash of the rail graph: each node and each connection list gets an independent hash (identified by nodeId) folded by order-independent addition (ElementHash, shared with train, belt and fluid)
    // サーバー(IRailNode)とクライアント(RailNodeInitializationData)の2入力が同じ要素関数を通るので同じ値になる。接続リストは target・distance 順に正規化してから取り込む
    // The server (IRailNode) and client (RailNodeInitializationData) inputs go through the same element functions and therefore agree; connection lists are normalized by target and distance before being taken in
    public static class RailGraphHashCalculator
    {
        private const uint NodeSeed = 0x7261696C;
        private const uint EdgeSeed = 0x65646765;

        public static uint ComputeGraphStateHash(IReadOnlyList<IRailNode> railNodes, List<List<(int targetId, int distance)>> connectNodes, Func<int, int, Guid> resolveRailType, Func<int, int, bool> resolveRailDrawable)
        {
            unchecked
            {
                uint sum = 0;
                if (railNodes != null)
                {
                    for (var nodeId = 0; nodeId < railNodes.Count; nodeId++)
                    {
                        var node = railNodes[nodeId];
                        if (node == null) continue;
                        sum += NodeElement(nodeId, node.NodeGuid, node.ConnectionDestination, node.FrontControlPoint.OriginalPosition, node.FrontControlPoint.ControlPointPosition, node.BackControlPoint.ControlPointPosition);
                    }
                }
                return sum + ConnectionElements(connectNodes, resolveRailType, resolveRailDrawable);
            }
        }

        public static uint ComputeGraphStateHash(IReadOnlyList<RailNodeInitializationData> nodes, IReadOnlyList<IReadOnlyList<(int targetId, int distance)>> connectNodes, Func<int, int, Guid> resolveRailType, Func<int, int, bool> resolveRailDrawable)
        {
            unchecked
            {
                uint sum = 0;
                if (nodes != null)
                {
                    for (var i = 0; i < nodes.Count; i++)
                    {
                        var node = nodes[i];
                        sum += NodeElement(node.NodeId, node.NodeGuid, node.ConnectionDestination, node.OriginPoint, node.FrontControlPoint, node.BackControlPoint);
                    }
                }
                return sum + ConnectionElements(connectNodes, resolveRailType, resolveRailDrawable);
            }
        }

        // Guid の無いノードは要素に数えない(両入力で同じ扱い)
        // A node without a Guid contributes nothing (the same on both inputs)
        private static uint NodeElement(int nodeId, Guid guid, ConnectionDestination destination, Vector3 origin, Vector3 frontControlPoint, Vector3 backControlPoint)
        {
            if (guid == Guid.Empty) return 0;

            var hash = ElementHash.Begin(NodeSeed, nodeId);
            hash = ElementHash.Add(hash, guid);
            hash = AddConnectionDestination(hash, destination);
            hash = AddVector3(hash, origin);
            hash = AddVector3(hash, frontControlPoint);
            hash = AddVector3(hash, backControlPoint);
            return ElementHash.Finish(hash);
        }

        private static uint ConnectionElements(IReadOnlyList<IReadOnlyList<(int targetId, int distance)>> connectNodes, Func<int, int, Guid> resolveRailType, Func<int, int, bool> resolveRailDrawable)
        {
            if (connectNodes == null || connectNodes.Count == 0) return 0;

            unchecked
            {
                uint sum = 0;
                for (var nodeId = 0; nodeId < connectNodes.Count; nodeId++)
                {
                    var edges = connectNodes[nodeId];
                    if (edges == null || edges.Count == 0) continue;

                    var normalized = new List<(int target, int dist)>(edges.Count);
                    for (var i = 0; i < edges.Count; i++) normalized.Add(edges[i]);
                    normalized.Sort((a, b) =>
                    {
                        var cmp = a.target.CompareTo(b.target);
                        return cmp != 0 ? cmp : a.dist.CompareTo(b.dist);
                    });

                    var hash = ElementHash.Begin(EdgeSeed, nodeId);
                    for (var i = 0; i < normalized.Count; i++)
                    {
                        hash = ElementHash.Add(hash, normalized[i].target);
                        hash = ElementHash.Add(hash, normalized[i].dist);
                        hash = ElementHash.Add(hash, resolveRailType == null ? Guid.Empty : resolveRailType(nodeId, normalized[i].target));
                        hash = ElementHash.Add(hash, resolveRailDrawable != null && resolveRailDrawable(nodeId, normalized[i].target));
                    }
                    sum += ElementHash.Finish(hash);
                }
                return sum;
            }
        }

        private static uint AddVector3(uint current, Vector3 vector)
        {
            var hash = ElementHash.Add(current, vector.x);
            hash = ElementHash.Add(hash, vector.y);
            return ElementHash.Add(hash, vector.z);
        }

        private static uint AddConnectionDestination(uint current, ConnectionDestination destination)
        {
            if (destination.IsDefault()) return ElementHash.Add(current, -1);

            var position = destination.blockPosition;
            var hash = ElementHash.Add(current, position.x);
            hash = ElementHash.Add(hash, position.y);
            hash = ElementHash.Add(hash, position.z);
            hash = ElementHash.Add(hash, destination.componentIndex);
            return ElementHash.Add(hash, destination.IsFront);
        }
    }
}
