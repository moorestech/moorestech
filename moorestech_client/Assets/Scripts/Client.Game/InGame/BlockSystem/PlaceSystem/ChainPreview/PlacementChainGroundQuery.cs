using Client.Game.InGame.BlockSystem.PlaceSystem.Ground;
using Game.Block.Interface;
using UnityEngine;

namespace Client.Game.InGame.BlockSystem.PlaceSystem.ChainPreview
{
    /// <summary>
    ///     通常設置と同じ地表解決（ADR 0047のfloor規則）で連結セルの整合を判定する本番実装
    ///     Production ground query using the same terrain resolution (ADR 0047 floor rule) as normal placement
    /// </summary>
    public class PlacementChainGroundQuery : IChainGroundQuery
    {
        public bool IsGroundAligned(Vector3Int cell, BlockDirection direction, Vector3Int blockSize)
        {
            // 地表が取れないセルは設置不能。取れたYが連結Yとズレるなら埋まりか浮きで、後から置くブロックが目標セルへ届かない
            // A cell without ground cannot host a block; a mismatched Y means buried or floating, so the later block never reaches the target cell
            if (!PlacementGroundCellResolver.TryResolveCellFromGround(cell, direction, blockSize, 0, out var resolved)) return false;
            return resolved.y == cell.y;
        }
    }
}
