using System;
using System.Collections.Generic;
using UnityEngine;

namespace Client.Game.InGame.ColliderStreaming
{
    /// <summary>
    /// 抽象ターゲットをチャンク単位に登録し、アクティブチャンク集合の差分だけをオンオフに反映する中央テーブル
    /// Central table: buckets targets by chunk and toggles only the delta of the active-chunk set
    /// </summary>
    public sealed class ColliderCullingChunkTable
    {
        private static readonly long[] EmptyChunks = Array.Empty<long>();

        private readonly float _chunkSize;

        // チャンク→登録エントリ群。大きなコライダーは重なる全チャンクに重複登録される
        // chunk -> entries. large colliders are registered into every chunk they overlap
        private readonly Dictionary<long, List<Entry>> _chunkToEntries = new();
        private readonly HashSet<long> _activeChunks = new();
        private bool _initialized;

        // 直近の評価に使ったプレイヤー中心と有効半径（初期化後の即時登録判定に使う）
        // Last player center/radius used for evaluation (drives immediate decision on post-init registration)
        private Vector3 _lastCenter;
        private float _lastRadius;

        // 毎フレームのGCを避ける使い回しバッファ
        // reusable buffers to avoid per-frame GC
        private readonly List<long> _chunkBuffer = new();
        private readonly HashSet<long> _nextActive = new();

        public ColliderCullingChunkTable(float chunkSize)
        {
            _chunkSize = chunkSize;
        }

        // 対象を登録し解除ハンドルを返す。初期化後は現在のアクティブ集合で即オンオフ判定する
        // Register a target and return a removal handle. After init, decide on/off immediately against the active set
        public IDisposable Add(Bounds worldBounds, IColliderDistanceCullingTarget target)
        {
            ColliderCullingChunkUtil.CollectOverlappingChunks(worldBounds, _chunkSize, _chunkBuffer);
            var chunkKeys = _chunkBuffer.Count == 0 ? EmptyChunks : _chunkBuffer.ToArray();

            var entry = new Entry(chunkKeys, target);
            foreach (var key in chunkKeys)
            {
                if (!_chunkToEntries.TryGetValue(key, out var list))
                {
                    list = new List<Entry>();
                    _chunkToEntries[key] = list;
                }
                list.Add(entry);

                // 初期化後は直近中心で範囲内判定し、新規占有チャンクはアクティブ集合へ加えて差分計算と整合させる
                // After init, test the last center; add newly occupied in-range chunks to the active set so delta stays consistent
                if (_initialized && ColliderCullingChunkUtil.ChunkWithinRadius(key, _lastCenter, _lastRadius, _chunkSize))
                {
                    _activeChunks.Add(key);
                    entry.ActiveChunkCount++;
                }
            }

            if (_initialized) entry.SetActive(entry.ActiveChunkCount > 0);
            return new Registration(this, entry);
        }

        // プレイヤー位置と有効半径から新アクティブ集合を求め、切り替わるチャンクのエントリだけをオンオフする
        // Compute the new active set from player pos/radius and toggle only entries in chunks that flip
        public void UpdateForCenter(Vector3 center, float radius)
        {
            _lastCenter = center;
            _lastRadius = radius;

            // 初回は占有チャンクを全走査して遠方offも含めた初期状態を確定する
            // First pass scans all occupied chunks to establish the initial state, including far-off entries
            if (!_initialized)
            {
                InitializeForCenter(center, radius);
                return;
            }

            BuildNextActive(center, radius);

            // 新規にアクティブ化したチャンク：参照カウントを増やし0→1で点灯
            // Newly active chunks: raise refcount, light up on 0->1
            foreach (var key in _nextActive)
            {
                if (_activeChunks.Contains(key)) continue;
                foreach (var entry in _chunkToEntries[key])
                    if (++entry.ActiveChunkCount == 1) entry.SetActive(true);
            }

            // 非アクティブ化したチャンク：参照カウントを減らし1→0で消灯
            // Newly inactive chunks: lower refcount, turn off on 1->0
            foreach (var key in _activeChunks)
            {
                if (_nextActive.Contains(key)) continue;
                foreach (var entry in _chunkToEntries[key])
                    if (--entry.ActiveChunkCount == 0) entry.SetActive(false);
            }

            CommitNextActive();
        }

        private void InitializeForCenter(Vector3 center, float radius)
        {
            // アクティブ集合は差分計算と同一経路で求め、初回と以降で参照カウントがズレないようにする
            // Derive the active set via the same path as delta so refcounts never drift between first pass and later
            BuildNextActive(center, radius);

            // アクティブチャンクに含まれる分だけ各エントリの参照カウントを積む
            // Raise each entry's refcount once per active chunk it belongs to
            foreach (var key in _nextActive)
            foreach (var entry in _chunkToEntries[key])
                entry.ActiveChunkCount++;

            // 全占有エントリへ初期状態を適用（遠方offもここで確定。多重チャンクでもSetActiveは冪等）
            // Apply the initial state to every occupied entry (far-off turns off here too; SetActive is idempotent)
            foreach (var pair in _chunkToEntries)
            foreach (var entry in pair.Value)
                entry.SetActive(entry.ActiveChunkCount > 0);

            CommitNextActive();
            _initialized = true;
        }

        private void BuildNextActive(Vector3 center, float radius)
        {
            _nextActive.Clear();
            ColliderCullingChunkUtil.CollectChunksInRadiusBox(center, radius, _chunkSize, _chunkBuffer);
            foreach (var key in _chunkBuffer)
            {
                if (!_chunkToEntries.ContainsKey(key)) continue;
                if (ColliderCullingChunkUtil.ChunkWithinRadius(key, center, radius, _chunkSize)) _nextActive.Add(key);
            }
        }

        private void CommitNextActive()
        {
            _activeChunks.Clear();
            foreach (var key in _nextActive) _activeChunks.Add(key);
        }

        private void Remove(Entry entry)
        {
            foreach (var key in entry.ChunkKeys)
            {
                if (!_chunkToEntries.TryGetValue(key, out var list)) continue;
                list.Remove(entry);
                if (list.Count != 0) continue;
                _chunkToEntries.Remove(key);
                _activeChunks.Remove(key);
            }
        }

        private sealed class Entry
        {
            public readonly long[] ChunkKeys;
            public readonly IColliderDistanceCullingTarget Target;
            public int ActiveChunkCount;
            private bool _active = true; // 既定はon（シーン配置時の状態）/ default on (as authored in scene)

            public Entry(long[] chunkKeys, IColliderDistanceCullingTarget target)
            {
                ChunkKeys = chunkKeys;
                Target = target;
            }

            // 状態が変わる時だけターゲットへ指示する（具体処理はターゲット側）
            // Instruct the target only when state changes (concrete work lives in the target)
            public void SetActive(bool on)
            {
                if (_active == on) return;
                _active = on;
                Target.SetCollider(on);
            }
        }

        private sealed class Registration : IDisposable
        {
            private readonly ColliderCullingChunkTable _table;
            private readonly Entry _entry;
            private bool _disposed;

            public Registration(ColliderCullingChunkTable table, Entry entry)
            {
                _table = table;
                _entry = entry;
            }

            public void Dispose()
            {
                if (_disposed) return;
                _disposed = true;
                _table.Remove(_entry);
            }
        }
    }
}
