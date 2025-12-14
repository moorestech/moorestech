using System;
using System.Collections.Generic;
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
    internal static class RailGraphHashCalculator
    {
        // 32bit FNV-1a 定数
        private const uint FnvOffset = 2166136261;
        private const uint FnvPrime = 16777619;

        /// <summary>
        /// railNodes / connectNodes の状態から順序独立ハッシュを計算する。
        /// </summary>
        public static uint ComputeGraphStateHash(List<RailNode> railNodes, List<List<(int targetId, int distance)>> connectNodes)
        {
            uint hash = FnvOffset;

            if (railNodes != null)
            {
                for (int nodeId = 0; nodeId < railNodes.Count; nodeId++)
                {
                    var node = railNodes[nodeId];
                    if (node == null)
                        continue;

                    hash = Mix(hash, unchecked((int)0x17F1_5C3D) ^ nodeId);
                    hash = MixGuid(hash, node.Guid);
                    hash = MixConnectionDestination(hash, node.ConnectionDestination);
                    hash = MixVector3(hash, node.FrontControlPoint.OriginalPosition);
                    hash = MixVector3(hash, node.FrontControlPoint.ControlPointPosition);
                    hash = MixVector3(hash, node.BackControlPoint.ControlPointPosition);
                }
            }

            hash = Mix(hash, unchecked((int)0x3F6A_2B1D));

            if (connectNodes != null && connectNodes.Count > 0)
            {
                for (int nodeId = 0; nodeId < connectNodes.Count; nodeId++)
                {
                    var edges = connectNodes[nodeId];
                    if (edges == null || edges.Count == 0)
                    {
                        // 接続がない場合はスキップ
                        continue;
                    }

                    var normalized = new List<(int target, int dist)>(edges);
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
                    }
                }
            }

            return hash;
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

            var position = destination.railComponentID.Position;
            current = Mix(current, position.x);
            current = Mix(current, position.y);
            current = Mix(current, position.z);
            current = Mix(current, destination.railComponentID.ID);
            current = Mix(current, destination.IsFront ? 1 : 0);
            return current;
        }

        private static int FloatToInt(float value)
        {
            return BitConverter.ToInt32(BitConverter.GetBytes(value), 0);
        }
    }
}
