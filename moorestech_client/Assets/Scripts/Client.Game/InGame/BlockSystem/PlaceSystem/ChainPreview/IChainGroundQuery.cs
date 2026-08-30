using Game.Block.Interface;
using UnityEngine;

namespace Client.Game.InGame.BlockSystem.PlaceSystem.ChainPreview
{
    /// <summary>
    ///     連結セルが地表と揃っているかを問い合わせる窓口。埋まり・浮きの両方を不成立として返す
    ///     The query port asking whether a chain cell sits level with the ground; buried and floating cells both fail
    /// </summary>
    public interface IChainGroundQuery
    {
        bool IsGroundAligned(Vector3Int cell, BlockDirection direction, Vector3Int blockSize);
    }
}
