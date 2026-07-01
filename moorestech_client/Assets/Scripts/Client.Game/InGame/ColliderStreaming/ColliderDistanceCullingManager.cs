using System;
using System.Collections.Generic;
using Client.Game.InGame.Player;
using UnityEngine;
using VContainer.Unity;

namespace Client.Game.InGame.ColliderStreaming
{
    /// <summary>
    /// 抽象ターゲットをチャンク単位で管理し、プレイヤー近傍だけをオンにする距離カリングの中核
    /// Core distance culling: buckets abstract targets into chunks and keeps only those near the player on
    /// </summary>
    public sealed class ColliderDistanceCullingManager : ITickable
    {
        // チャンク一辺(m)。有効半径はブロック選択レイ(100m)+カメラ最大距離(10m)+再評価移動量(8m)を包含
        // Chunk size (m). Active radius covers pick ray (100m) + max camera boom (10m) + re-eval move (8m)
        private const float ChunkSize = 32f;
        private const float ActiveRadius = 120f;
        private const float ReevalMoveDistance = 8f;

        private readonly PlayerSystemContainer _playerSystemContainer;

        // チャンク→登録エントリ群（中央テーブル）
        // chunk -> registered entries (the central table)
        private readonly Dictionary<long, List<Entry>> _chunkToEntries = new();

        private Vector3 _lastEvalPosition;
        private bool _initialized;

        public ColliderDistanceCullingManager(PlayerSystemContainer playerSystemContainer)
        {
            _playerSystemContainer = playerSystemContainer;
        }

        // 対象を登録し、解除用のハンドルを返す（登録/解除の主体は各サービスクラス）
        // Register a target and return a handle for removal (register/unregister is driven by service classes)
        public IDisposable Register(Vector3 worldPosition, IColliderDistanceCullingTarget target)
        {
            var chunk = ChunkKeyOf(worldPosition);
            var entry = new Entry(chunk, target);
            if (!_chunkToEntries.TryGetValue(chunk, out var list))
            {
                list = new List<Entry>();
                _chunkToEntries[chunk] = list;
            }
            list.Add(entry);

            // ストリーミング開始済みなら現在位置で即判定（既定はon＝従来挙動）
            // If streaming has started, decide now against the current position (default is on = legacy behavior)
            if (_initialized)
            {
                var player = _playerSystemContainer.PlayerObjectController;
                if (player != null) SetEntry(entry, ChunkWithinRadius(chunk, player.Position));
            }

            return new Registration(this, entry);
        }

        // プレイヤーが一定距離動いた時だけ全チャンクを距離で再評価する（S3戦略）
        // Re-evaluate all chunks by distance only after the player moves a set distance (S3 strategy)
        public void Tick()
        {
            var player = _playerSystemContainer.PlayerObjectController;
            if (player == null) return;

            var center = player.Position;
            if (_initialized && (center - _lastEvalPosition).sqrMagnitude <= ReevalMoveDistance * ReevalMoveDistance) return;
            _lastEvalPosition = center;
            _initialized = true;

            foreach (var pair in _chunkToEntries)
            {
                var on = ChunkWithinRadius(pair.Key, center);
                foreach (var entry in pair.Value) SetEntry(entry, on);
            }
        }

        private void Remove(Entry entry)
        {
            if (!_chunkToEntries.TryGetValue(entry.ChunkKey, out var list)) return;
            list.Remove(entry);
            if (list.Count == 0) _chunkToEntries.Remove(entry.ChunkKey);
        }

        // 状態が変わった時だけ対象へオンオフを指示する（具体処理はしない）
        // Instruct the target on/off only when the state changes (no concrete work here)
        private static void SetEntry(Entry entry, bool on)
        {
            if (entry.Active == on) return;
            entry.Active = on;
            entry.Target.SetCollider(on);
        }

        // チャンクの最近点がプレイヤーから半径内かを厳密に判定する
        // Exact test: whether the chunk's nearest point is within the radius of the player
        private static bool ChunkWithinRadius(long chunkKey, Vector3 center)
        {
            var cx = (int)(chunkKey >> 32);
            var cz = (int)(chunkKey & 0xffffffff);
            var minX = cx * ChunkSize;
            var minZ = cz * ChunkSize;
            var nearestX = Mathf.Clamp(center.x, minX, minX + ChunkSize);
            var nearestZ = Mathf.Clamp(center.z, minZ, minZ + ChunkSize);
            var dx = nearestX - center.x;
            var dz = nearestZ - center.z;
            return dx * dx + dz * dz <= ActiveRadius * ActiveRadius;
        }

        private static long ChunkKeyOf(Vector3 worldPos)
        {
            return PackChunk(Mathf.FloorToInt(worldPos.x / ChunkSize), Mathf.FloorToInt(worldPos.z / ChunkSize));
        }

        private static long PackChunk(int cx, int cz)
        {
            return ((long)cx << 32) ^ (uint)cz;
        }

        // 1登録分の管理データ
        // Bookkeeping for a single registration
        private sealed class Entry
        {
            public readonly long ChunkKey;
            public readonly IColliderDistanceCullingTarget Target;
            public bool Active;

            public Entry(long chunkKey, IColliderDistanceCullingTarget target)
            {
                ChunkKey = chunkKey;
                Target = target;
                Active = true;
            }
        }

        // 解除ハンドル。Disposeで中央テーブルから外す
        // Removal handle. Dispose removes the entry from the central table
        private sealed class Registration : IDisposable
        {
            private readonly ColliderDistanceCullingManager _manager;
            private readonly Entry _entry;
            private bool _disposed;

            public Registration(ColliderDistanceCullingManager manager, Entry entry)
            {
                _manager = manager;
                _entry = entry;
            }

            public void Dispose()
            {
                if (_disposed) return;
                _disposed = true;
                _manager.Remove(_entry);
            }
        }
    }
}
