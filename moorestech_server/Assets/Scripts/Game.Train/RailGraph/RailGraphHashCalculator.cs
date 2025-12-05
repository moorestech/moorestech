using System;
using System.Collections.Generic;

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
        /// connectNodes の状態から順序独立ハッシュを計算する。
        /// </summary>
        public static uint ComputeHash(List<List<(int targetId, int distance)>> connectNodes)
        {
            if (connectNodes == null || connectNodes.Count == 0)
                return 0;

            uint hash = FnvOffset;

            for (int nodeId = 0; nodeId < connectNodes.Count; nodeId++)
            {
                var edges = connectNodes[nodeId];

                if (edges == null || edges.Count == 0)
                {
                    // ノードが存在し接続がない場合も状態に含める
                    hash = Mix(hash, nodeId);
                    continue;
                }

                // 内部順序が保証されていない場合のために、一時コピーして並べ替え
                // （targetId -> distance の順序比較）
                var normalized = new List<(int target, int dist)>(edges);
                normalized.Sort((a, b) =>
                {
                    int cmp = a.target.CompareTo(b.target);
                    return cmp != 0 ? cmp : a.dist.CompareTo(b.dist);
                });

                // ノード境界を示すseparator（衝突低減）
                hash = Mix(hash, unchecked((int)0x7F00) ^ nodeId);

                // エッジ情報を順に混合
                for (int i = 0; i < normalized.Count; i++)
                {
                    hash = Mix(hash, normalized[i].target);
                    hash = Mix(hash, normalized[i].dist);
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
    }
}
