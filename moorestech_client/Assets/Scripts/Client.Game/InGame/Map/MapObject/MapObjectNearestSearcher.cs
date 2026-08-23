using System;
using System.Collections.Generic;
using Client.Game.InGame.Map.NearestSearch;
using UnityEngine;

namespace Client.Game.InGame.Map.MapObject
{
    /// <summary>
    ///     mapObjectGuid別の最寄り探索。破壊はdirtyで受け、次の探索時に生存個体だけで索引を組み直す
    ///     Nearest search per mapObjectGuid; destruction marks the guid dirty and the next search rebuilds the index from live objects only
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

        public MapObjectGameObject SearchNearest(Guid mapObjectGuid, Vector3 position)
        {
            if (_dirtyGuids.Remove(mapObjectGuid)) RebuildIndex(mapObjectGuid);
            return _nearestIndex.SearchNearest(mapObjectGuid, position);
        }

        private void RebuildIndex(Guid mapObjectGuid)
        {
            // 可否の判断はここで行い、索引には生存個体の座標だけを渡す
            // Availability is decided here; the index receives only live objects' positions
            _availableBuffer.Clear();
            foreach (var mapObject in _mapObjectsByGuid[mapObjectGuid])
            {
                if (mapObject.IsAvailable) _availableBuffer.Add(mapObject);
            }

            _nearestIndex.SetTargets(mapObjectGuid, _availableBuffer);
        }
    }
}
