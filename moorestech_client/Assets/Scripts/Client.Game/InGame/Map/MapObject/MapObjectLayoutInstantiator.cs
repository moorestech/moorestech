using System;
using System.Collections.Generic;
using Client.Common.Asset;
using Core.Master;
using Server.Protocol.PacketResponse;
using Server.Protocol.PacketResponse.MapData;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Client.Game.InGame.Map.MapObject
{
    /// <summary>
    ///     layoutから個体生成しスナップショット適用・索引登録
    ///     Instantiates a map object from a layout, applies its snapshot, and registers it for search
    /// </summary>
    public sealed class MapObjectLayoutInstantiator
    {
        private readonly Transform _parent;
        private readonly Dictionary<int, MapObjectGameObject> _allMapObjects;
        private readonly Dictionary<int, GetMapObjectInfoProtocol.MapObjectsInfoMessagePack> _snapshotByInstanceId;
        private readonly MapObjectNearestSearcher _nearestSearcher;
        private readonly MapObjectPendingStateLedger _pendingStateLedger;
        private readonly Dictionary<Guid, GameObject> _prefabCacheByMapObjectGuid = new();

        public MapObjectLayoutInstantiator(
            Transform parent,
            Dictionary<int, MapObjectGameObject> allMapObjects,
            Dictionary<int, GetMapObjectInfoProtocol.MapObjectsInfoMessagePack> snapshotByInstanceId,
            MapObjectNearestSearcher nearestSearcher,
            MapObjectPendingStateLedger pendingStateLedger)
        {
            _parent = parent;
            _allMapObjects = allMapObjects;
            _snapshotByInstanceId = snapshotByInstanceId;
            _nearestSearcher = nearestSearcher;
            _pendingStateLedger = pendingStateLedger;
        }

        public void InstantiateFromLayout(MapObjectLayoutMessagePack layout)
        {
            // guidは正常データ前提でparseする（不正guidはT8のデータ修正対象・ここでの防御は過剰）
            // Parse guid assuming valid data (malformed guids are a T8 data fix; defending here is overkill)
            var mapObjectGuid = new Guid(layout.MapObjectGuid);

            // 失敗個体はskipし続行
            // Skip this instance on failure and keep going
            var prefab = ResolvePrefabOrNull(mapObjectGuid);
            if (prefab == null) return;

            // スナップショット欠落はInstantiate前に検出し、orphan instanceを作らずskipする
            // Detect a missing snapshot before Instantiate so no orphan instance is created, then skip
            if (!_snapshotByInstanceId.TryGetValue(layout.InstanceId, out var snapshot))
            {
                Debug.LogError($"MapObject snapshot missing. InstanceId:{layout.InstanceId} MapObjectGuid:{mapObjectGuid}");
                return;
            }

            // 生成時のRotation/Scaleを実インスタンスへ戻す。既定値のままだと全個体が同じ向きで直立し裸地も生成時サイズで広がる
            // Restore the generated rotation and scale; the defaults face every instance alike and spread bare ground at the generated size
            var rotation = new Quaternion(layout.RotationX, layout.RotationY, layout.RotationZ, layout.RotationW);
            var instance = Object.Instantiate(prefab, new Vector3(layout.X, layout.Y, layout.Z), rotation, _parent);
            instance.transform.localScale = new Vector3(layout.ScaleX, layout.ScaleY, layout.ScaleZ);

            // rootにMapObjectGameObjectが無いのはprefab authoring不正。生成物を破棄してskipする
            // Missing MapObjectGameObject on root is invalid prefab authoring; destroy the instance and skip
            var mapObject = instance.GetComponent<MapObjectGameObject>();
            if (mapObject == null)
            {
                Debug.LogError($"MapObject prefab has no MapObjectGameObject on root. MapObjectGuid:{mapObjectGuid}");
                Object.Destroy(instance);
                return;
            }

            // instanceId重複はTryAddで検出し、重複個体を破棄してskipする（Addのthrowは起動ハングを招くため不可）
            // Detect duplicate instanceId via TryAdd; destroy the duplicate and skip (Add's throw would hang startup)
            mapObject.SetRuntimeIdentity(layout.InstanceId, layout.MapObjectGuid);
            if (!_allMapObjects.TryAdd(layout.InstanceId, mapObject))
            {
                Debug.LogError($"MapObject duplicate InstanceId:{layout.InstanceId} MapObjectGuid:{mapObjectGuid}");
                Object.Destroy(instance);
                return;
            }

            // 登録後に初期状態を適用
            // Apply the initial state after registration
            mapObject.Initialize(snapshot);

            // 生成前に届いた破壊/HPイベントをスナップショットより優先して適用する（ADR 0030）
            // Apply destroy/HP events that arrived before instantiation, overriding the snapshot (ADR 0030)
            if (_pendingStateLedger.TryConsume(layout.InstanceId, out var pendingState))
            {
                if (pendingState.HasHp) mapObject.UpdateHp(pendingState.Hp);
                if (pendingState.IsDestroyed && !mapObject.IsDestroyed) mapObject.DestroyMapObject();
            }

            // 最寄り探索の候補へ登録
            // Register as a nearest-search candidate
            _nearestSearcher.Register(mapObject);

            #region Internal

            GameObject ResolvePrefabOrNull(Guid guid)
            {
                // 失敗もnullとしてキャッシュする。同一guidが千個規模で並ぶため同期loadとLogErrorはguidごと1回に抑える
                // Failures are cached as null too; a guid can repeat by the thousand so keep the sync load and LogError once per guid
                if (_prefabCacheByMapObjectGuid.TryGetValue(guid, out var cachedPrefab)) return cachedPrefab;

                // master欠落はskip対象
                // Master-missing is skipped
                var element = MasterHolder.MapObjectMaster.GetMapObjectElementOrNull(guid);
                if (element == null)
                {
                    Debug.LogError($"MapObject master missing. MapObjectGuid:{guid}");
                    _prefabCacheByMapObjectGuid[guid] = null;
                    return null;
                }

                // load失敗もskip対象
                // Load failure is also skipped
                var loaded = AddressableLoader.LoadDefault<GameObject>(element.AddressablePath);
                if (loaded == null)
                {
                    Debug.LogError($"MapObject prefab load failed. MapObjectGuid:{guid} AddressablePath:{element.AddressablePath}");
                    _prefabCacheByMapObjectGuid[guid] = null;
                    return null;
                }

                _prefabCacheByMapObjectGuid[guid] = loaded;
                return loaded;
            }

            #endregion
        }
    }
}
