using Game.Block.Interface;
using UnityEngine;

namespace Client.Game.InGame.BlockSystem.PlaceSystem.ChainPreview
{
    /// <summary>
    ///     連結セルの地表整合を問い合わせる窓口
    ///     The query port asking whether a chain cell sits level with the ground; buried and floating cells both fail
    /// </summary>
    public interface IChainGroundQuery
    {
        bool IsGroundAligned(Vector3Int cell, BlockDirection direction, Vector3Int blockSize);
    }
}
