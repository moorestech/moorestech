using UnityEngine;

namespace Client.Game.InGame.BlockSystem.PlaceSystem.BeltConveyor.World
{
    /// <summary>
    ///     セルにベルトを置けない理由。端点の判定（セッション）と途中セルの判定（探索規則）が同じ1本を通る
    ///     Why a cell cannot take a belt; endpoint checks (session) and mid-route checks (search rule) share this one judgement
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
