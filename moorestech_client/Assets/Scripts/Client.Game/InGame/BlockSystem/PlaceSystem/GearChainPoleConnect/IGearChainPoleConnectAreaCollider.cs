using System.Collections.Generic;
using Client.Game.InGame.BlockSystem.StateProcessor;
using UnityEngine;

namespace Client.Game.InGame.BlockSystem.PlaceSystem.GearChainPoleConnect
{
    /// <summary>
    /// GearChainPoleブロックのレイキャスト検出用インターフェース
    /// Interface for detecting GearChainPole blocks via raycast
    /// </summary>
    public interface IGearChainPoleConnectAreaCollider : IBlockGameObjectInnerComponent
    {
        /// <summary>
        /// ブロックの位置（ワールド座標）
        /// Block position in world coordinates
        /// </summary>
        Vector3Int Position { get; }

        /// <summary>
        /// 最大接続距離
        /// Maximum connection distance
        /// </summary>
        float MaxConnectionDistance { get; }

        /// <summary>
        /// 接続数が上限に達しているかどうか
        /// Whether the connection count has reached its limit
        /// </summary>
        bool IsConnectionFull { get; }

        /// <summary>
        /// 既存の接続先ポール位置一覧
        /// List of connected partner pole positions
        /// </summary>
        IReadOnlyList<Vector3Int> ConnectedPolePositions { get; }

        /// <summary>
        /// 指定位置のポールと接続されているかどうか
        /// Whether this pole is connected to the pole at the specified position
        /// </summary>
        bool ContainsConnection(Vector3Int partnerPosition);
    }
}
