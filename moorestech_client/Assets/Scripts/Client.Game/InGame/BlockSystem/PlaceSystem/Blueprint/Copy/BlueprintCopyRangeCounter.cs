using System.Collections.Generic;
using Game.Block.Interface;
using Game.Blueprint;
using Mooresmaster.Model.BlocksModule;
using UnityEngine;

namespace Client.Game.InGame.BlockSystem.PlaceSystem.Blueprint.Copy
{
    /// <summary>
    ///     サーバーの抽出規則で範囲内のブロックを数える
    ///     Counts blocks in a range with the server extraction rule
    /// </summary>
    public static class BlueprintCopyRangeCounter
    {
        public static int Count(IEnumerable<(BlockMasterElement master, BlockPositionInfo position)> blocks, Vector3Int min, Vector3Int max)
        {
            var box = BlueprintCopyTargetRule.CreateBox(min, max);
            var count = 0;
            foreach (var (master, position) in blocks)
            {
                if (!BlueprintCopyTargetRule.IsCopiedByBox(master, position, box)) continue;
                count++;
            }

            return count;
        }
    }
}
