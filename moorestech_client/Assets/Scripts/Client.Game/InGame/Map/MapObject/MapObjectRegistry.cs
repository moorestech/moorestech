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

            // 生成前に届いた破壊/HPを最寄り索引へ載せる前に当てる。順序が逆だと破壊済み個体がdirty無しで探索候補に残る
            // Pending destroy/HP lands before the nearest index takes the object; the reverse order would leave a destroyed object searchable without a dirty mark
            if (_pendingStateLedger.TryConsume(mapObject.InstanceId, out var pendingState))
                MapObjectPendingStateApplier.Apply(mapObject, pendingState);

            _nearestSearcher.Register(mapObject);
            return true;
        }

        public bool TryGet(int instanceId, out MapObjectGameObject mapObject)
        {
            return _mapObjectsByInstanceId.TryGetValue(instanceId, out mapObject);
        }

        public bool TryDestroy(int instanceId)
        {
            // 破壊とdirtyは対でしか成立しない。外へ出して手組みさせると片方を落とした呼び出しを型が拒めない
            // Destruction and the dirty mark hold only as a pair; exposing them lets a caller drop one and the type cannot refuse it
            if (!_mapObjectsByInstanceId.TryGetValue(instanceId, out var mapObject)) return false;

            mapObject.DestroyMapObject();
            _nearestSearcher.MarkDirty(mapObject.MapObjectGuid);
            return true;
        }

        public void RecordPendingDestroy(int instanceId)
        {
            _pendingStateLedger.RecordDestroy(instanceId);
        }

        public void RecordPendingHp(int instanceId, int hp)
        {
            _pendingStateLedger.RecordHp(instanceId, hp);
        }

        public MapObjectGameObject SearchNearest(Guid mapObjectGuid, Vector3 position)
        {
            return _nearestSearcher.SearchNearest(mapObjectGuid, position);
        }
    }
}
