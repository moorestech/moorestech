using System;
using System.Collections.Generic;
using Client.Game.InGame.Map.NearestSearch;
using UnityEngine;

namespace Client.Game.InGame.Map.MapObject
{
    /// <summary>
    ///     - mapObjectGuid別の最寄り索引を候補集合で横断探索
    ///     - 破壊はdirtyで受ける
    ///     - 探索時に生存個体で再構築
    ///     - Nearest index per mapObjectGuid, searched across a candidate set
    ///     - Destruction marks the guid dirty
    ///     - Rebuilds from live objects on search
    /// </summary>
    public sealed class MapObjectNearestSearcher
    {
        private readonly Dictionary<Guid, List<MapObjectGameObject>> _mapObjectsByGuid = new();
        private readonly HashSet<Guid> _dirtyGuids = new();
        private readonly NearestTargetIndex<MapObjectGameObject> _nearestIndex = new();

        // 再構築時の生存個体バッファ。索引側が配列へ複製するので使い回せる
        // Live-object buffer for rebuilds; the index copies into its own arrays, so this can be reused
        private readonly List<MapObjectGameObject> _availableBuffer = new();

        public void Register(MapObjectGameObject mapObject)
        {
            var guid = mapObject.MapObjectGuid;
            if (!_mapObjectsByGuid.TryGetValue(guid, out var mapObjects))
            {
                mapObjects = new List<MapObjectGameObject>();
                _mapObjectsByGuid.Add(guid, mapObjects);
            }

            // 生成はフレーム分散なので登録のたびにdirtyにし、最初の探索で一括構築する
            // Instantiation is spread across frames, so mark dirty per registration and build once on the first search
            mapObjects.Add(mapObject);
            _dirtyGuids.Add(guid);
        }

        public void MarkDirty(Guid mapObjectGuid)
        {
            _dirtyGuids.Add(mapObjectGuid);
        }

        public MapObjectGameObject SearchNearest(HashSet<Guid> mapObjectGuids, Vector3 position)
        {
            // 候補guidごとに独立した索引を引き、その中の最寄りを選ぶ
            // Query the independent index of each candidate guid and pick the nearest among them
            MapObjectGameObject nearest = null;
            var nearestSqrMagnitude = float.MaxValue;

            foreach (var mapObjectGuid in mapObjectGuids)
            {
                if (_dirtyGuids.Remove(mapObjectGuid)) RebuildIndex(mapObjectGuid);

                var candidate = _nearestIndex.SearchNearest(mapObjectGuid, position);
                if (candidate == null) continue;

                var sqrMagnitude = (position - candidate.Position).sqrMagnitude;
                if (nearestSqrMagnitude <= sqrMagnitude) continue;

                nearest = candidate;
                nearestSqrMagnitude = sqrMagnitude;
            }

            return nearest;

            #region Internal

            void RebuildIndex(Guid guid)
            {
                // 未登録guidのdirtyは無視する。MarkDirtyの受理域がRegister済みguidより広いため
                // Ignore a dirty mark for an unregistered guid; MarkDirty accepts more than the registered set
                if (!_mapObjectsByGuid.TryGetValue(guid, out var mapObjects)) return;

                // 可否の判断はここで行い、索引には生存個体の座標だけを渡す
                // Availability is decided here; the index receives only live objects' positions
                // IsAvailableは採掘可否の述語だが、ピンが指せる対象の条件と同義なので流用する
                // IsAvailable is the mining predicate, reused here because it matches what a pin can point at
                _availableBuffer.Clear();
                foreach (var mapObject in mapObjects)
                {
                    if (mapObject.IsAvailable) _availableBuffer.Add(mapObject);
                }

                _nearestIndex.SetTargets(guid, _availableBuffer);
            }

            #endregion
        }
    }
}
