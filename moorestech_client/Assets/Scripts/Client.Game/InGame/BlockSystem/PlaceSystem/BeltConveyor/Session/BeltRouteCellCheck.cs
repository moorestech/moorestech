using Client.Game.InGame.Block;
using Client.Game.InGame.BlockSystem.PlaceSystem.BeltConveyor.Route;
using Client.Game.InGame.BlockSystem.PlaceSystem.BeltConveyor.World;
using Client.Game.InGame.BlockSystem.PlaceSystem.Ground;
using Game.Block.Interface;
using Game.Block.Interface.Extension;
using UnityEngine;

namespace Client.Game.InGame.BlockSystem.PlaceSystem.BeltConveyor.Session
{
    /// <summary>
    ///     始点・終点のセルが置けない理由。途中セルの判定は探索規則が持つ
    ///     Why a start or end cell cannot take a belt; mid-route cells are judged by the search rule
    /// </summary>
    public enum BeltRouteCellBlockReason
    {
        None,
        ExistingBlock,
        Terrain,
        SlopeHead,
        OutOfRange,
    }

    /// <summary>
    ///     端点セルの可否を、箱がある間は箱から、箱が無い間は実ワールドから判定する
    ///     Judges endpoint cells from the box while one exists, and from the live world before the start is fixed
    /// </summary>
    public static class BeltRouteCellCheck
    {
        public static BeltRouteCellBlockReason JudgeInGrid(BeltRouteGrid grid, Vector3Int cell)
        {
            if (!grid.Contains(cell)) return BeltRouteCellBlockReason.OutOfRange;
            if (grid.IsBlockedByBlock(cell)) return BeltRouteCellBlockReason.ExistingBlock;
            if (grid.IsBlockedByTerrain(cell)) return BeltRouteCellBlockReason.Terrain;
            if (grid.IsSlopeHead(cell)) return BeltRouteCellBlockReason.SlopeHead;
            return BeltRouteCellBlockReason.None;
        }

        // 始点確定前はまだ箱が無いので、重なり・埋まり・真下の坂を実ワールドで見る
        // Before the start is fixed there is no box, so overlap, sinking and a slope underneath are read from the live world
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
