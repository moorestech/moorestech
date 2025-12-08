using UnityEngine;

namespace Client.Game.InGame.BlockSystem.PlaceSystem.GearChainPoleConnect
{
    /// <summary>
    /// GearChainPoleの接続対象を識別するためのインターフェース
    /// Interface to identify GearChainPole connection targets
    /// </summary>
    public interface IGearChainPoleConnectAreaCollider
    {
        /// <summary>
        /// ブロックの元座標を取得する
        /// Get the original block position
        /// </summary>
        Vector3Int GetBlockPosition();
    }
}
