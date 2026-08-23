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
    ///     layout1件からmapObject個体を生成し、スナップショット・保留イベントの適用と索引登録まで担う
    ///     Instantiates one map object from a layout, applying its snapshot and pending events and registering it for search
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

            // master欠落・load失敗はResolvePrefabOrNull内でLogError済み。個体だけskipし残りは生成しきる
            // Master-missing or load-failure is already logged inside; skip just this one and keep generating the rest
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

            // 登録後にスナップショットで初期状態（破壊/HP）を適用する
            // Apply the initial state (destroy/HP) from the snapshot after registration
            mapObject.Initialize(snapshot);

            // 生成前に届いた破壊/HPイベントをスナップショットより優先して適用する（ADR 0030）
            // Apply destroy/HP events that arrived before instantiation, overriding the snapshot (ADR 0030)
            if (_pendingStateLedger.TryConsume(layout.InstanceId, out var pendingState))
            {
                if (pendingState.HasHp) mapObject.UpdateHp(pendingState.Hp);
                if (pendingState.IsDestroyed && !mapObject.IsDestroyed) mapObject.DestroyMapObject();
            }

            // 最寄り探索の候補へ登録する（破壊済みは探索時の生存フィルタで除かれる）
            // Register as a nearest-search candidate (destroyed ones drop out at the live filter on search)
            _nearestSearcher.Register(mapObject);
        }

        private GameObject ResolvePrefabOrNull(Guid mapObjectGuid)
        {
            // 失敗もnullとしてキャッシュする。同一guidが千個規模で並ぶため同期loadとLogErrorはguidごと1回に抑える
            // Failures are cached as null too; a guid can repeat by the thousand so keep the sync load and LogError once per guid
            if (_prefabCacheByMapObjectGuid.TryGetValue(mapObjectGuid, out var cachedPrefab)) return cachedPrefab;

            // master欠落はLogError+nullでskipさせる（サーバMapObjectDatastoreと対称）
            // Master-missing returns null after LogError to skip (symmetric with server MapObjectDatastore)
            var element = MasterHolder.MapObjectMaster.GetMapObjectElementOrNull(mapObjectGuid);
            if (element == null)
            {
                Debug.LogError($"MapObject master missing. MapObjectGuid:{mapObjectGuid}");
                _prefabCacheByMapObjectGuid[mapObjectGuid] = null;
                return null;
            }

            // load失敗（有料アセット不在等）もLogError+nullでskipさせる
            // Load failure (e.g. missing paid asset) also returns null after LogError to skip
            var loaded = AddressableLoader.LoadDefault<GameObject>(element.AddressablePath);
            if (loaded == null)
            {
                Debug.LogError($"MapObject prefab load failed. MapObjectGuid:{mapObjectGuid} AddressablePath:{element.AddressablePath}");
                _prefabCacheByMapObjectGuid[mapObjectGuid] = null;
                return null;
            }

            _prefabCacheByMapObjectGuid[mapObjectGuid] = loaded;
            return loaded;
        }
    }
}
