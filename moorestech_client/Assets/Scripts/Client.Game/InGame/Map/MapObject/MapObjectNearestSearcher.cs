using System;
using System.Collections.Generic;
using Client.Game.InGame.Map.NearestSearch;
using UniRx;
using UnityEngine;

namespace Client.Game.InGame.Map.MapObject
{
    /// <summary>
    ///     - mapObjectGuid別の最寄り索引を候補集合で横断探索
    ///     - 破壊は個体の変化通知を購読して索引へ伝える
    ///     - Nearest index per mapObjectGuid, searched across a candidate set
    ///     - Destruction reaches the index through each object's change notification
    /// </summary>
    internal sealed class MapObjectNearestSearcher
    {
        private readonly NearestTargetIndex<MapObjectGameObject> _nearestIndex = new();

        public void Register(MapObjectGameObject mapObject)
        {
            var mapObjectGuid = mapObject.MapObjectGuid;
            _nearestIndex.Register(mapObjectGuid, mapObject);

            // 破壊は呼び出し側の手押しではなく変化通知で受ける（押し忘れが索引の嘘になるため）
            // Destruction arrives through the change notification rather than a manual push, since a missed push would make the index lie
            mapObject.OnDestroyMapObject.Subscribe(_ => _nearestIndex.NotifyTargetUnsearchable(mapObjectGuid));
        }

        public MapObjectGameObject SearchNearest(HashSet<Guid> mapObjectGuids, Vector3 position)
        {
            // 候補guidごとに独立した索引を引き、索引が返した距離のまま横断比較する
            // Query the independent index of each candidate guid and compare across them with the distance the index returned
            MapObjectGameObject nearest = null;
            var nearestSqrDistance = float.MaxValue;

            foreach (var mapObjectGuid in mapObjectGuids)
            {
                if (!_nearestIndex.TrySearchNearest(mapObjectGuid, position, out var candidate, out var sqrDistance)) continue;
                if (nearestSqrDistance <= sqrDistance) continue;

                nearest = candidate;
                nearestSqrDistance = sqrDistance;
            }

            return nearest;
        }
    }
}
