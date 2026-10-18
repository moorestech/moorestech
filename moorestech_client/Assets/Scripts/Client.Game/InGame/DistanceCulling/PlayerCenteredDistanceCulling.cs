using System;
using Client.Game.InGame.Player;
using UnityEngine;

namespace Client.Game.InGame.DistanceCulling
{
    /// <summary>
    /// プレイヤーが一定距離動くたびにチャンクテーブルへ半径内/外の差分を指示する
    /// Each time the player moves a set distance, drives the chunk table's within/outside-range delta
    /// </summary>
    public sealed class PlayerCenteredDistanceCulling
    {
        private readonly PlayerSystemContainer _playerSystemContainer;
        private readonly DistanceCullingChunkTable _chunkTable;
        private readonly float _radius;
        private readonly float _reevalMoveDistance;

        private Vector3 _lastEvalPosition;
        private bool _hasEvaluated;

        internal PlayerCenteredDistanceCulling(PlayerSystemContainer playerSystemContainer, float chunkSize, float radius, float reevalMoveDistance)
        {
            _playerSystemContainer = playerSystemContainer;
            _chunkTable = new DistanceCullingChunkTable(chunkSize);
            _radius = radius;
            _reevalMoveDistance = reevalMoveDistance;
        }

        internal IDisposable Register(Bounds worldBounds, IDistanceCullingTarget target)
        {
            return _chunkTable.Add(worldBounds, target);
        }

        internal void Tick()
        {
            var player = _playerSystemContainer.PlayerObjectController;
            if (player == null) return; // silent-ok: プレイヤー生成前の数フレームだけ / 生成後のTickで評価が走り録画テストで確認

            var center = player.Position;
            // silent-ok: 移動閾値以内は直前の評価を維持 / 閾値を超えたTickで再評価される
            // Keep the previous evaluation until movement exceeds the threshold
            if (_hasEvaluated && (center - _lastEvalPosition).sqrMagnitude <= _reevalMoveDistance * _reevalMoveDistance) return;
            _hasEvaluated = true;
            _lastEvalPosition = center;

            _chunkTable.UpdateForCenter(center, _radius);
        }
    }
}
