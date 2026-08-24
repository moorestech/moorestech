using System;
using System.Collections.Generic;
using Client.Game.InGame.Map.MapObject.Pending;
using UnityEngine;

namespace Client.Game.InGame.Map.MapObject
{
    /// <summary>
    ///     生成済みmapObjectの登録簿。instanceId索引・最寄り探索索引・未生成宛の保留台帳を1つの型で束ねる
    ///     The registry of instantiated map objects, binding the instanceId map, the nearest-search index and the pending ledger into one type
    /// </summary>
    public sealed class MapObjectRegistry
    {
        private readonly Dictionary<int, MapObjectGameObject> _mapObjectsByInstanceId = new();
        private readonly MapObjectNearestSearcher _nearestSearcher = new();
        private readonly MapObjectPendingStateLedger _pendingStateLedger = new();

        public bool TryRegister(MapObjectGameObject mapObject)
        {
            // instanceId重複はここで弾く。生の索引を外へ出さないので登録手順を呼び出し側が組み替えられない
            // Duplicate instanceIds are rejected here; the raw indexes never leave, so no caller can reorder the registration steps
            if (!_mapObjectsByInstanceId.TryAdd(mapObject.InstanceId, mapObject)) return false;

            // 生成前に届いた破壊/HPは最寄り索引へ載せる前に当てる。索引は登録時点の座標を焼き込むので、破壊済みは最初の構築から外す
            // Pending destroy/HP lands before the nearest index takes the object: the index bakes the position at registration, so a destroyed one stays out of the very first build
            if (_pendingStateLedger.TryConsume(mapObject.InstanceId, out var pendingState))
                MapObjectPendingStateApplier.Apply(mapObject, pendingState);

            _nearestSearcher.Register(mapObject);
            return true;
        }

        public void ApplyDestroy(int instanceId)
        {
            if (!TryDestroy(instanceId)) RecordPendingDestroy(instanceId);
        }

        public void ApplyHp(int instanceId, int hp)
        {
            if (TryGet(instanceId, out var mapObject)) mapObject.UpdateHp(hp);
            else RecordPendingHp(instanceId, hp);
        }

        internal void DiscardPendingState(int instanceId)
        {
            _pendingStateLedger.Discard(instanceId);
        }

        private bool TryGet(int instanceId, out MapObjectGameObject mapObject)
        {
            return _mapObjectsByInstanceId.TryGetValue(instanceId, out mapObject);
        }

        private bool TryDestroy(int instanceId)
        {
            // 破壊通知は索引へ届くため生存だけ裁く
            // The destroy event updates the index, so the registry only judges liveness
            if (!TryGet(instanceId, out var mapObject)) return false;
            if (mapObject.IsDestroyed) return true;

            mapObject.DestroyMapObject();
            return true;
        }

        private void RecordPendingDestroy(int instanceId)
        {
            _pendingStateLedger.RecordDestroy(instanceId);
        }

        private void RecordPendingHp(int instanceId, int hp)
        {
            _pendingStateLedger.RecordHp(instanceId, hp);
        }

        public MapObjectGameObject SearchNearest(HashSet<Guid> mapObjectGuids, Vector3 position)
        {
            return _nearestSearcher.SearchNearest(mapObjectGuids, position);
        }
    }
}
