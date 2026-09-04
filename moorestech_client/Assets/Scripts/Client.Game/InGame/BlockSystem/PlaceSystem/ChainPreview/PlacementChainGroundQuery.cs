using Client.Game.InGame.BlockSystem.PlaceSystem.Ground;
using Game.Block.Interface;
using UnityEngine;

namespace Client.Game.InGame.BlockSystem.PlaceSystem.ChainPreview
{
    /// <summary>
    ///     通常設置と同じ地表解決で連結セルを判定する本番実装
    ///     Production ground query using the same terrain resolution (ADR 0047 floor rule) as normal placement
    /// </summary>
    public class PlacementChainGroundQuery : IChainGroundQuery
    {
        public ChainCellBlockReason ResolveGroundAlignment(Vector3Int cell, BlockDirection direction, Vector3Int blockSize, int heightOffset)
        {
            // 地表が取れないセルは設置不能。通常設置と同じheightOffsetを噛ませ、E/Qで上げた段でもYが一致する
            // A cell without ground cannot host a block; the same heightOffset as normal placement keeps Y consistent under E/Q
            if (!PlacementGroundCellResolver.TryResolveCellFromGround(cell, direction, blockSize, heightOffset, out var resolved)) return ChainCellBlockReason.GroundNotFound;
            return resolved.y == cell.y ? ChainCellBlockReason.None : ChainCellBlockReason.GroundHeightMismatch;
        }
    }
}
