using System;
using System.Collections.Generic;
using Client.Game.InGame.Block;
using UniRx;
using UnityEngine;
using VContainer.Unity;

namespace Client.Game.InGame.ColliderStreaming.Block
{
    /// <summary>
    /// ブロックの設置/削除に合わせて距離カリング対象を登録・解除するサービス
    /// Registers/unregisters distance-culling targets as blocks are placed/removed
    /// </summary>
    public sealed class BlockColliderCullingRegisterService : IStartable, IDisposable
    {
        private readonly BlockGameObjectDataStore _dataStore;
        private readonly ColliderDistanceCullingManager _manager;
        private readonly CompositeDisposable _disposables = new();

        // ブロック位置→解除ハンドル
        // block position -> removal handle
        private readonly Dictionary<Vector3Int, IDisposable> _registrations = new();

        public BlockColliderCullingRegisterService(BlockGameObjectDataStore dataStore, ColliderDistanceCullingManager manager)
        {
            _dataStore = dataStore;
            _manager = manager;
        }

        public void Start()
        {
            // 設置・削除を購読し、既に存在するブロックも登録する
            // Subscribe to place/remove, and register already-existing blocks
            _dataStore.OnBlockPlaced.Subscribe(Register).AddTo(_disposables);
            _dataStore.OnBlockRemoved.Subscribe(Unregister).AddTo(_disposables);
            foreach (var block in _dataStore.BlockGameObjectDictionary.Values) Register(block);
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
            if (_registrations.TryGetValue(pos, out var old))
            {
                old.Dispose();
                _registrations.Remove(pos);
            }

            // ブロック配下のコライダーを対象化し、AABBでチャンクをまたぐ大きなブロックも網羅して登録する
            // Wrap the block's colliders as a target and register with an AABB so multi-chunk blocks are covered
            var colliders = block.GetComponentsInChildren<Collider>(true);
            var target = new BlockColliderCullingTarget(colliders);
            var bounds = CalcWorldBounds(colliders, block.transform.position);
            _registrations[pos] = _manager.Register(bounds, target);

            #region Internal

            Bounds CalcWorldBounds(Collider[] targetColliders, Vector3 fallbackCenter)
            {
                var encapsulated = false;
                var result = new Bounds(fallbackCenter, Vector3.zero);
                foreach (var collider in targetColliders)
                {
                    if (collider == null) continue;
                    if (!encapsulated)
                    {
                        result = collider.bounds;
                        encapsulated = true;
                    }
                    else
                    {
                        result.Encapsulate(collider.bounds);
                    }
                }
                return result;
            }

            #endregion
        }

        private void Unregister(Vector3Int pos)
        {
            if (!_registrations.TryGetValue(pos, out var handle)) return;
            handle.Dispose();
            _registrations.Remove(pos);
        }
    }
}
