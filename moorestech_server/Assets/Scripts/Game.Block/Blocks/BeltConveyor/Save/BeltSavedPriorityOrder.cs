using System.Collections.Generic;
using Core.BeltTransport;
using Game.Block.Interface;
using UnityEngine;

namespace Game.Block.Blocks.BeltConveyor.Save
{
    // 保存済みの優先順を使えるか決める。合流は搬出方向、分岐は搬入方向を除いた3方向の並べ替えだけを受け入れる
    // Decides whether a saved priority order is usable: only a permutation of the three directions other than the merge output or the branch input is accepted
    public static class BeltSavedPriorityOrder
    {
        public static int Resolve(IReadOnlyDictionary<BlockInstanceId, int> savedPriorityOrders, BlockInstanceId blockInstanceId, BeltDirection excludedDirection)
        {
            if (!savedPriorityOrders.TryGetValue(blockInstanceId, out var saved)) return BeltPriority.InitializeFromDirection;
            if (IsPermutation(saved, excludedDirection)) return saved;

            // 壊れた優先順は異常。向きからの初期値で代える
            // A corrupted order is an anomaly; fall back to the direction-based initial order
            Debug.LogError($"[BeltTransport] belt block {blockInstanceId} has an unreadable saved priority order {saved}; initializing it from the direction.");
            return BeltPriority.InitializeFromDirection;
        }

        private static bool IsPermutation(int order, BeltDirection excludedDirection)
        {
            // 2bitずつ3つの方向を読み、除外方向を含まず重複も無いこと。上位bitの余りも許さない
            // Read three 2-bit directions; none may be the excluded one or repeat, and no bits may remain above them
            if (order < 0 || (order >> 6) != 0) return false;
            var seen = 0;
            for (var rank = 0; rank < 3; rank++)
            {
                var direction = BeltPriority.Direction(order, rank);
                if (direction == (int)excludedDirection || (seen & (1 << direction)) != 0) return false;
                seen |= 1 << direction;
            }
            return true;
        }
    }
}
