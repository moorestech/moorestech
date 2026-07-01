using System;
using System.Collections.Generic;
using Client.Game.InGame.Player;
using UniRx;
using UnityEngine;
using VContainer.Unity;

namespace Client.Game.InGame.Block.ColliderStreaming
{
    /// <summary>
    /// ブロックのコライダーをチャンク単位でプレイヤー近傍だけ有効化するストリーマ
    /// Streams block colliders, keeping only chunks near the player enabled
    /// </summary>
    public class BlockColliderChunkStreamer : IStartable, ITickable, IDisposable
    {
        // チャンク一辺(m)。有効化半径はブロック選択レイ(100m)+カメラ距離(最大10m)+再評価移動量(8m)より広く取り選択不能を防ぐ
        // Chunk size (m). Active radius covers pick ray (100m) + max camera boom (10m) + re-eval move (8m) so picking never breaks
        private const float ChunkSize = 32f;
        private const float ActiveRadius = 120f;
        private const float ReevalMoveDistance = 8f;

        private readonly BlockGameObjectDataStore _dataStore;
        private readonly PlayerSystemContainer _playerSystemContainer;
        private readonly CompositeDisposable _disposables = new();

        // 位置→管理ブロック、チャンク→管理ブロック群（中央テーブル）
        // pos->block and chunk->blocks (the central table)
        private readonly Dictionary<Vector3Int, ManagedBlock> _blocks = new();
        private readonly Dictionary<long, List<ManagedBlock>> _chunkToBlocks = new();

        private Vector3 _lastEvalPosition;
        private bool _initialized;

        public BlockColliderChunkStreamer(BlockGameObjectDataStore dataStore, PlayerSystemContainer playerSystemContainer)
        {
            _dataStore = dataStore;
            _playerSystemContainer = playerSystemContainer;
        }

        public void Start()
        {
            // 設置・削除を購読し、既に存在するブロックも登録する
            // Subscribe to place/remove, and register already-existing blocks
            _dataStore.OnBlockPlaced.Subscribe(Register).AddTo(_disposables);
            _dataStore.OnBlockRemoved.Subscribe(Unregister).AddTo(_disposables);
            foreach (var block in _dataStore.BlockGameObjectDictionary.Values) Register(block);
        }

        // プレイヤーが一定距離動いた時だけ全チャンクを距離で再評価する（S3戦略）
        // Re-evaluate all chunks by distance only after the player moves a set distance (S3 strategy)
        public void Tick()
        {
            var player = _playerSystemContainer.PlayerObjectController;
            if (player == null) return;

            // 定常フレームは移動量チェックのみ。閾値未満なら何もしない
            // Steady frames only check the move distance; do nothing below the threshold
            var center = player.Position;
            if (_initialized && (center - _lastEvalPosition).sqrMagnitude <= ReevalMoveDistance * ReevalMoveDistance) return;
            _lastEvalPosition = center;
            _initialized = true;

            // 全チャンクを半径判定し、状態が変わるブロックのみトグルする
            // Test every chunk against the radius; SetBlockColliders only toggles blocks whose state changes
            foreach (var pair in _chunkToBlocks)
            {
                var on = ChunkWithinRadius(pair.Key, center);
                foreach (var managed in pair.Value) SetBlockColliders(managed, on);
            }
        }

        public void Dispose()
        {
            _disposables.Dispose();
        }

        private void Register(BlockGameObject block)
        {
            var pos = block.BlockPosInfo.OriginalPos;

            // 置き換え等で既登録の場合は先に解除する
            // Unregister first if already registered (e.g. block replacement)
            if (_blocks.ContainsKey(pos)) Unregister(pos);

            // ブロック配下のコライダーを集める
            // Collect colliders under the block
            var colliders = block.GetComponentsInChildren<Collider>(true);
            var chunk = ChunkKeyOf(block.transform.position);
            var managed = new ManagedBlock(chunk, colliders);

            _blocks[pos] = managed;
            if (!_chunkToBlocks.TryGetValue(chunk, out var list))
            {
                list = new List<ManagedBlock>();
                _chunkToBlocks[chunk] = list;
            }
            list.Add(managed);

            // ストリーミング開始済みなら現在のプレイヤー位置で即判定（既定は有効＝従来挙動）
            // If streaming has started, decide now against the current player position (default is enabled = legacy behavior)
            if (!_initialized) return;
            var player = _playerSystemContainer.PlayerObjectController;
            if (player != null) SetBlockColliders(managed, ChunkWithinRadius(chunk, player.Position));
        }

        private void Unregister(Vector3Int pos)
        {
            if (!_blocks.TryGetValue(pos, out var managed)) return;
            _blocks.Remove(pos);

            if (!_chunkToBlocks.TryGetValue(managed.ChunkKey, out var list)) return;
            list.Remove(managed);
            if (list.Count == 0) _chunkToBlocks.Remove(managed.ChunkKey);
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

        // 無効化時に現在の有効状態を保存し、有効化時はそれを復元する（実行時のトグルを尊重）
        // On disable save the current enabled state, on enable restore it (respects runtime toggles)
        private static void SetBlockColliders(ManagedBlock managed, bool on)
        {
            if (managed.CollidersActive == on) return;
            managed.CollidersActive = on;

            var colliders = managed.Colliders;
            var savedEnabled = managed.SavedEnabled;
            for (var i = 0; i < colliders.Length; i++)
            {
                var collider = colliders[i];
                if (collider == null) continue;

                if (on)
                {
                    collider.enabled = savedEnabled[i];
                }
                else
                {
                    savedEnabled[i] = collider.enabled;
                    collider.enabled = false;
                }
            }
        }

        private static long ChunkKeyOf(Vector3 worldPos)
        {
            return PackChunk(Mathf.FloorToInt(worldPos.x / ChunkSize), Mathf.FloorToInt(worldPos.z / ChunkSize));
        }

        private static long PackChunk(int cx, int cz)
        {
            return ((long)cx << 32) ^ (uint)cz;
        }

        // 1ブロック分の管理データ
        // Managed data for a single block
        private sealed class ManagedBlock
        {
            public readonly long ChunkKey;
            public readonly Collider[] Colliders;
            public readonly bool[] SavedEnabled;
            public bool CollidersActive;

            public ManagedBlock(long chunkKey, Collider[] colliders)
            {
                ChunkKey = chunkKey;
                Colliders = colliders;
                SavedEnabled = new bool[colliders.Length];
                CollidersActive = true;
            }
        }
    }
}
