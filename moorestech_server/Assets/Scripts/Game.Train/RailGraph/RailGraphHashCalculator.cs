using System;
using System.Collections.Generic;
using Game.Train.SaveLoad;
using UnityEngine;

namespace Game.Train.RailGraph
{
    /// <summary>
    /// RailGraphDatastore 内部の connectNodes(List<List<(int,int)>>) から
    /// 決定的かつ順序非依存のハッシュ値を生成するユーティリティ。
    ///
    /// ・connectNodes[i] 内の順序が異なっていても同内容なら同じハッシュ
    /// ・FNV-1aベースで軽量＆高速
    /// ・ネットワーク同期/差分検出を目的とした運用前提
    /// </summary>
    public static class RailGraphHashCalculator
    {
        // 32bit FNV-1a 定数
        private const uint FnvOffset = 2166136261;
        private const uint FnvPrime = 16777619;

        /// <summary>
        /// railNodes / connectNodes の状態から順序独立ハッシュを計算する。
        /// </summary>
        public static uint ComputeGraphStateHash(IReadOnlyList<IRailNode> railNodes, List<List<(int targetId, int distance)>> connectNodes, Func<int, int, Guid> resolveRailType)
        {
            uint hash = FnvOffset;

            if (railNodes != null)
            {
                for (int nodeId = 0; nodeId < railNodes.Count; nodeId++)
                {
                    IRailNode node = railNodes[nodeId];
                    if (node == null)
                        continue;

                    hash = MixNode(
                        hash,
                        nodeId,
                        node.NodeGuid,
                        node.ConnectionDestination,
                        node.FrontControlPoint.OriginalPosition,
                        node.FrontControlPoint.ControlPointPosition,
                        node.BackControlPoint.ControlPointPosition);
                }
            }

            hash = Mix(hash, unchecked((int)0x3F6A_2B1D));
            return MixConnections(hash, connectNodes, resolveRailType);
        }

        /// <summary>
        /// RailNodeInitializationData / connectNodes から順序独立ハッシュを計算する。
        /// </summary>
        public static uint ComputeGraphStateHash(
            IReadOnlyList<RailNodeInitializationData> nodes,
            IReadOnlyList<IReadOnlyList<(int targetId, int distance)>> connectNodes,
            Func<int, int, Guid> resolveRailType)
        {
            uint hash = FnvOffset;

            if (nodes != null)
            {
                for (int i = 0; i < nodes.Count; i++)
                {
                    var node = nodes[i];
                    hash = MixNode(
                        hash,
                        node.NodeId,
                        node.NodeGuid,
                        node.ConnectionDestination,
                        node.OriginPoint,
                        node.FrontControlPoint,
                        node.BackControlPoint);
                }
            }

            hash = Mix(hash, unchecked((int)0x3F6A_2B1D));
            return MixConnections(hash, connectNodes, resolveRailType);
        }

        /// <summary>
        /// FNV-1a mix
        /// </summary>
        private static uint Mix(uint current, int value)
        {
            unchecked
            {
                uint v = (uint)value;
                current ^= v;
                current *= FnvPrime;
                return current;
            }
        }

        private static uint MixGuid(uint current, Guid guid)
        {
            var bytes = guid.ToByteArray();
            for (int i = 0; i < bytes.Length; i += 4)
            {
                int chunk = BitConverter.ToInt32(bytes, i);
                current = Mix(current, chunk);
            }
            return current;
        }

        private static uint MixVector3(uint current, Vector3 vector)
        {
            current = Mix(current, FloatToInt(vector.x));
            current = Mix(current, FloatToInt(vector.y));
            current = Mix(current, FloatToInt(vector.z));
            return current;
        }

        private static uint MixConnectionDestination(uint current, ConnectionDestination destination)
        {
            if (destination.IsDefault())
            {
                return Mix(current, -1);
            }

            var position = destination.blockPosition;
            current = Mix(current, position.x);
            current = Mix(current, position.y);
            current = Mix(current, position.z);
            current = Mix(current, destination.componentIndex);
            current = Mix(current, destination.IsFront ? 1 : 0);
            return current;
        }

        private static int FloatToInt(float value)
        {
            return BitConverter.ToInt32(BitConverter.GetBytes(value), 0);
        }

        private static uint MixNode(
            uint current,
            int nodeId,
            Guid guid,
            ConnectionDestination destination,
            Vector3 origin,
            Vector3 frontControlPoint,
            Vector3 backControlPoint)
        {
            if (guid == Guid.Empty)
                return current;

            uint hash = Mix(current, unchecked((int)0x17F1_5C3D) ^ nodeId);
            hash = MixGuid(hash, guid);
            hash = MixConnectionDestination(hash, destination);
            hash = MixVector3(hash, origin);
            hash = MixVector3(hash, frontControlPoint);
            hash = MixVector3(hash, backControlPoint);
            return hash;
        }

        private static uint MixConnections(
            uint current,
            IReadOnlyList<IReadOnlyList<(int targetId, int distance)>> connectNodes,
            Func<int, int, Guid> resolveRailType)
        {
            if (connectNodes == null || connectNodes.Count == 0)
                return current;

            uint hash = current;
            for (int nodeId = 0; nodeId < connectNodes.Count; nodeId++)
            {
                var edges = connectNodes[nodeId];
                if (edges == null || edges.Count == 0)
                    continue;

                var normalized = new List<(int target, int dist)>(edges.Count);
                for (int i = 0; i < edges.Count; i++)
                {
                    normalized.Add(edges[i]);
                }
                normalized.Sort((a, b) =>
                {
                    int cmp = a.target.CompareTo(b.target);
                    return cmp != 0 ? cmp : a.dist.CompareTo(b.dist);
                });

                hash = Mix(hash, unchecked((int)0x7F00) ^ nodeId);
                for (int i = 0; i < normalized.Count; i++)
                {
                    hash = Mix(hash, normalized[i].target);
                    hash = Mix(hash, normalized[i].dist);
                    hash = MixGuid(hash, ResolveRailTypeGuid(resolveRailType, nodeId, normalized[i].target));
                }
            }

            return hash;
        }

        private static Guid ResolveRailTypeGuid(Func<int, int, Guid> resolveRailType, int startNodeId, int endNodeId)
        {
            return resolveRailType == null ? Guid.Empty : resolveRailType(startNodeId, endNodeId);
        }
    }
}
