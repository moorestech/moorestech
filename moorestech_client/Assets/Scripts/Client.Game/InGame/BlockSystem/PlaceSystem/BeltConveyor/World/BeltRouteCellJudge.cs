using UnityEngine;

namespace Client.Game.InGame.BlockSystem.PlaceSystem.BeltConveyor.World
{
    /// <summary>
    ///     セルにベルトを置けない理由（端点・途中で共通）
    ///     Why a cell cannot take a belt (shared by endpoints and mid-route)
    /// </summary>
    public enum BeltRouteCellBlockReason
    {
        None,
        ExistingBlock,
        Terrain,
        SlopeHead,
    }

    public static class BeltRouteCellJudge
    {
        public static BeltRouteCellBlockReason Judge(IBeltRouteWorld world, Vector3Int cell)
        {
            if (world.IsBlockedByBlock(cell)) return BeltRouteCellBlockReason.ExistingBlock;
            if (world.IsBlockedByTerrain(cell)) return BeltRouteCellBlockReason.Terrain;
            if (world.IsSlopeHead(cell)) return BeltRouteCellBlockReason.SlopeHead;
            return BeltRouteCellBlockReason.None;
        }
    }
}
