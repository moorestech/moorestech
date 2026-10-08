using System.Collections.Generic;
using Game.Block.Interface;
using Game.Block.Interface.Extension;
using Mooresmaster.Model.BlocksModule;
using UnityEngine;
using static Mooresmaster.Model.BlocksModule.BlockMasterElement;

namespace Game.Blueprint
{
    /// <summary>
    ///     サーバー抽出とクライアント計数が共用するコピー対象規則
    ///     Copy-target rules shared by server extraction and client counting
    /// </summary>
    public static class BlueprintCopyTargetRule
    {
        // レール系のグラフはブロック状態の外にある
        // Rail graph state lives outside block state
        private static readonly HashSet<string> ExcludedBlockTypes = new()
        {
            BlockTypeConst.TrainRail,
            BlockTypeConst.TrainStation,
            BlockTypeConst.TrainItemPlatform,
            BlockTypeConst.TrainFluidPlatform,
        };

        public static bool IsCopyTarget(BlockMasterElement master)
        {
            return !ExcludedBlockTypes.Contains(master.BlockType);
        }

        // 占有セルが一つでも範囲に入れば対象とする
        // Include a block when any occupied cell intersects the box
        public static bool IntersectsBox(BlockPositionInfo positionInfo, Vector3Int min, Vector3Int max)
        {
            var box = new BlockPositionInfo(min, BlockDirection.North, max - min + Vector3Int.one);
            return positionInfo.IsOverlap(box);
        }
    }
}
