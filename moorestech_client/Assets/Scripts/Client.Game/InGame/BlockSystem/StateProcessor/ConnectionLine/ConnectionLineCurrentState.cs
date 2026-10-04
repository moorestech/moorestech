using Client.Game.InGame.Block;
using UnityEngine;

namespace Client.Game.InGame.BlockSystem.StateProcessor.ConnectionLine
{
    /// <summary>
    ///     現在の端点座標をブロックIDへ解決し、表示中の接続線索引を照合する
    ///     Resolves current endpoint positions to block IDs and checks the displayed line index
    /// </summary>
    public sealed class ConnectionLineCurrentState : IConnectionLineCurrentState
    {
        private readonly BlockGameObjectDataStore _blockStore;
        private readonly ConnectionLineRegistry _registry;

        public ConnectionLineCurrentState(BlockGameObjectDataStore blockStore, ConnectionLineRegistry registry)
        {
            _blockStore = blockStore;
            _registry = registry;
        }

        public bool HasConnection(ConnectionLineKind kind, Vector3Int posA, Vector3Int posB)
        {
            // 再設置要求はまだクライアントへ反映されないため、端点不在ならサーバー判定へ送る
            // A re-place request is not yet reflected on the client, so absent endpoints go to server validation
            if (!_blockStore.TryGetBlockGameObject(posA, out var blockA) ||
                !_blockStore.TryGetBlockGameObject(posB, out var blockB)) return false;

            return _registry.HasLineBetween(blockA.BlockInstanceId, blockB.BlockInstanceId, kind);
        }
    }
}
