using System;
using System.Collections.Generic;
using Client.Game.InGame.DistanceCulling;
using UniRx;
using UnityEngine;

namespace Client.Game.InGame.Block.DistanceCulling
{
    /// <summary>
    /// ブロックの設置/削除に合わせて距離カリングへ登録・解除する
    /// Registers/unregisters blocks with a distance culling as they are placed/removed
    /// </summary>
    public sealed class BlockDistanceCullingRegistrar
    {
        private readonly BlockGameObjectDataStore _dataStore;
        private readonly PlayerCenteredDistanceCulling _culling;
        private readonly IBlockDistanceCullingSource _source;
        private readonly Dictionary<Vector3Int, IDisposable> _registrations = new();

        public BlockDistanceCullingRegistrar(BlockGameObjectDataStore dataStore, PlayerCenteredDistanceCulling culling, IBlockDistanceCullingSource source)
        {
            _dataStore = dataStore;
            _culling = culling;
            _source = source;
        }

        public void Start()
        {
            // 設置・削除を購読し、既存ブロックも登録する
            // Subscribe to place/remove, and register existing blocks
            _dataStore.OnBlockPlaced.Subscribe(Register);
            _dataStore.OnBlockRemoved.Subscribe(Unregister);
            foreach (var block in _dataStore.BlockGameObjectDictionary.Values) Register(block);
        }

        private void Register(BlockGameObject block)
        {
            // 置き換え等で既登録なら先に解除する
            // Unregister first if already registered (e.g. block replacement)
            var pos = block.BlockPosInfo.OriginalPos;
            Unregister(pos);

            var entry = _source.CreateEntry(block);
            _registrations[pos] = _culling.Register(entry.WorldBounds, entry.Target);
        }

        private void Unregister(Vector3Int pos)
        {
            if (!_registrations.TryGetValue(pos, out var registration)) return; // silent-ok: 未登録位置の置換前解除は不要 / 新規登録は直後に実行する
            registration.Dispose();
            _registrations.Remove(pos);
        }
    }
}
