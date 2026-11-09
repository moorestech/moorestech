using Client.Game.InGame.Block;
using Client.Game.InGame.BlockSystem.PlaceSystem.BeltConveyor.World;
using Client.Game.InGame.BlockSystem.PlaceSystem.Ground;
using Game.Block.Interface;
using Game.Block.Interface.Extension;
using UnityEngine;

namespace Client.Game.InGame.BlockSystem.PlaceSystem.BeltConveyor.Session
{
    /// <summary>
    ///     始点確定前の端点可否を実ワールドで判定
    ///     Judges endpoint cells in the live world before the start is fixed
    /// </summary>
    public static class BeltRouteCellCheck
    {
        // 重なり・埋まり・真下の坂を実ワールドで見る
        // Overlap, sinking and a slope underneath are read from the live world
        public static BeltRouteCellBlockReason JudgeInWorld(BlockGameObjectDataStore blocks, Vector3Int cell)
        {
            if (blocks.IsOverlapPositionInfo(new BlockPositionInfo(cell, BlockDirection.North, Vector3Int.one))) return BeltRouteCellBlockReason.ExistingBlock;
            if (PlacementTerrainSinkQuery.IsSunk(cell, BlockDirection.North, Vector3Int.one)) return BeltRouteCellBlockReason.Terrain;
            if (IsAboveExistingSlope(blocks, cell)) return BeltRouteCellBlockReason.SlopeHead;
            return BeltRouteCellBlockReason.None;
        }

        private static bool IsAboveExistingSlope(BlockGameObjectDataStore blocks, Vector3Int cell)
        {
            if (!blocks.TryGetBlockGameObject(cell + Vector3Int.down, out var below)) return false;
            if (!BeltRouteBlockEdgeScanner.IsBelt(below.BlockMasterElement)) return false;
            if (!BeltConveyorPlaceFamilyUtil.TryGetFamily(below.BlockId, out var family)) return false;
            return family.TryGetSlopeDirection(below.BlockId, out _);
        }
    }
}
