using System;
using Client.Game.InGame.Player;
using UnityEngine;
using VContainer.Unity;

namespace Client.Game.InGame.ColliderStreaming
{
    /// <summary>
    /// プレイヤー位置を監視し、一定距離動くたびにチャンクテーブルへ差分オンオフを指示する距離カリングの中核
    /// Core distance culling: watches the player and drives delta on/off on the chunk table each time it moves enough
    /// </summary>
    public sealed class ColliderDistanceCullingManager : ITickable
    {
        // 有効半径はブロック選択レイ(100m)+カメラ最大距離(10m)+再評価移動量(8m)を包含
        // Active radius covers pick ray (100m) + max camera boom (10m) + re-eval move (8m)
        private const float ChunkSize = 32f;
        private const float ActiveRadius = 120f;
        private const float ReevalMoveDistance = 8f;

        private readonly PlayerSystemContainer _playerSystemContainer;
        private readonly ColliderCullingChunkTable _chunkTable = new(ChunkSize);

        private Vector3 _lastEvalPosition;
        private bool _hasEvaluated;

        public ColliderDistanceCullingManager(PlayerSystemContainer playerSystemContainer)
        {
            _playerSystemContainer = playerSystemContainer;
        }

        // 対象を登録し解除ハンドルを返す（登録/解除の主体は各サービスクラス）
        // Register a target and return a removal handle (register/unregister is driven by service classes)
        public IDisposable Register(Bounds worldBounds, IColliderDistanceCullingTarget target)
        {
            return _chunkTable.Add(worldBounds, target);
        }

        // プレイヤーが一定距離動いた時だけテーブルへ再評価を依頼する（S3戦略）
        // Ask the table to re-evaluate only after the player moves a set distance (S3 strategy)
        public void Tick()
        {
            var player = _playerSystemContainer.PlayerObjectController;
            if (player == null) return;

            var center = player.Position;
            if (_hasEvaluated && (center - _lastEvalPosition).sqrMagnitude <= ReevalMoveDistance * ReevalMoveDistance) return;
            _hasEvaluated = true;
            _lastEvalPosition = center;

            _chunkTable.UpdateForCenter(center, ActiveRadius);
        }
    }
}
