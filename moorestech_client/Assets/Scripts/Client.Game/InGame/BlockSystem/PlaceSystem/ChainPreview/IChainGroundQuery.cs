using Game.Block.Interface;
using UnityEngine;

namespace Client.Game.InGame.BlockSystem.PlaceSystem.ChainPreview
{
    /// <summary>
    ///     連結セルが地表と揃っているかの問い合わせ窓口
    ///     The query port asking whether a chain cell sits level with the ground; buried and floating cells both fail
    /// </summary>
    public interface IChainGroundQuery
    {
        // 地表なしと高さ不一致を弁別して返す。揃っていればNone
        // Distinguishes a missing ground from a height mismatch; None when the cell sits level
        ChainCellBlockReason ResolveGroundAlignment(Vector3Int cell, BlockDirection direction, Vector3Int blockSize, int heightOffset);
    }
}
