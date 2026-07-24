using System;
using System.Collections.Generic;
using System.Linq;
using Client.Common.Asset;
using Client.Game.InGame.Context;
using Client.Network.API;
using Core.Master;
using Cysharp.Threading.Tasks;
using MessagePack;
using Server.Event.EventReceive;
using UnityEngine;
using VContainer;

namespace Client.Game.InGame.Map.MapObject
{
    /// <summary>
    ///     mapObjectをLayout応答から実行時Instantiateし、破壊/HPの状態同期を担うデータストア
    ///     Instantiates map objects at runtime from the layout response and keeps their destroy/HP state synced
    /// </summary>
    public class MapObjectGameObjectDatastore : MonoBehaviour
    {
        private readonly Dictionary<int, MapObjectGameObject> _allMapObjects = new();
        private readonly Dictionary<Guid, GameObject> _prefabCacheByMapObjectGuid = new();

        [Inject]
        public void Construct(InitialHandshakeResponse handshakeResponse)
        {
            // イベント購読は同期で確定させ、生成本体はフレーム分散のfire-and-forgetへ委譲する
            // Subscribe synchronously, then delegate the instantiation itself to a frame-distributed fire-and-forget
            ClientContext.VanillaApi.Event.SubscribeEventResponse(MapObjectUpdateEventPacket.EventTag, OnUpdateMapObject);
            InstantiateMapObjectsFromLayoutAsync(handshakeResponse).Forget();
        }

        private async UniTask InstantiateMapObjectsFromLayoutAsync(InitialHandshakeResponse handshakeResponse)
        {
            // 破壊/HPの初期状態はva:mapObjectInfoスナップショットをinstanceIdで引く（Layoutと同一集合が前提）
            // Initial destroy/HP state comes from the va:mapObjectInfo snapshot keyed by instanceId (same set as the layout)
            var snapshotByInstanceId = handshakeResponse.MapObjects.ToDictionary(info => info.InstanceId);
            var cancellationToken = this.GetCancellationTokenOnDestroy();

            var processedCount = 0;
            foreach (var layout in handshakeResponse.MapLayout.MapObjects)
            {
                // guid→master→addressablePathでプレハブ解決し、Layout座標へ生成する
                // Resolve the prefab via guid→master→addressablePath and instantiate it at the layout position
                var mapObjectGuid = new Guid(layout.MapObjectGuid);
                var prefab = ResolvePrefab(mapObjectGuid);
                var instance = Instantiate(prefab, new Vector3(layout.X, layout.Y, layout.Z), Quaternion.identity, transform);

                var mapObject = instance.GetComponent<MapObjectGameObject>();
                if (mapObject == null) throw new InvalidOperationException($"MapObject prefab has no MapObjectGameObject on root. MapObjectGuid:{mapObjectGuid}");

                // 実行時IDを注入し、登録後にスナップショットで初期状態を適用する
                // Inject the runtime identity, then apply the initial state from the snapshot after registration
                mapObject.SetRuntimeIdentity(layout.InstanceId, layout.MapObjectGuid);
                _allMapObjects.Add(layout.InstanceId, mapObject);
                mapObject.Initialize(snapshotByInstanceId[layout.InstanceId]);

                // 2011個規模の起動スパイクを避けるため100個ごとにフレームを跨ぐ
                // Cross a frame every 100 objects to avoid a startup spike at the ~2011-object scale
                processedCount++;
                if (processedCount % 100 == 0) await UniTask.Yield(cancellationToken);
            }

            #region Internal

            GameObject ResolvePrefab(Guid mapObjectGuid)
            {
                if (_prefabCacheByMapObjectGuid.TryGetValue(mapObjectGuid, out var cachedPrefab)) return cachedPrefab;

                var element = MasterHolder.MapObjectMaster.GetMapObjectElement(mapObjectGuid);
                var loaded = AddressableLoader.LoadDefault<GameObject>(element.AddressablePath);
                if (loaded == null) throw new InvalidOperationException($"MapObject prefab load failed. AddressablePath:{element.AddressablePath}");

                _prefabCacheByMapObjectGuid[mapObjectGuid] = loaded;
                return loaded;
            }

            #endregion
        }

        private void OnUpdateMapObject(byte[] payLoad)
        {
            var data = MessagePackSerializer.Deserialize<MapObjectUpdateEventMessagePack>(payLoad);

            // 非同期Instantiate進行中で該当個体が未生成ならスキップ（データ欠損の吸収ではなくロード順序の許容）
            // Skip only while async instantiation hasn't reached this object yet (load-order tolerance, not data-defense)
            if (!_allMapObjects.TryGetValue(data.InstanceId, out var mapObject)) return;

            switch (data.EventType)
            {
                case MapObjectUpdateEventMessagePack.DestroyEventType:
                    mapObject.DestroyMapObject();
                    break;
                case MapObjectUpdateEventMessagePack.HpUpdateEventType:
                    mapObject.UpdateHp(data.CurrentHp);
                    break;
                default:
                    throw new Exception("MapObjectUpdateEventProtocol: EventTypeが不正か実装されていません");
            }
        }

        public MapObjectGameObject SearchNearestMapObject(Guid mapObjectGuid, Vector3 position)
        {
            MapObjectGameObject nearestMapObject = null;
            var maxMagnitude = float.MaxValue;

            foreach (var mapObject in _allMapObjects.Values)
            {
                // 指定されているmapObjectか破壊されていないかチェック
                if (mapObject.MapObjectGuid != mapObjectGuid || mapObject.IsDestroyed) continue;

                // 距離をチェック
                var magnitude = (position - mapObject.GetPosition()).magnitude;
                if (maxMagnitude < magnitude) continue;

                nearestMapObject = mapObject;
                maxMagnitude = magnitude;
            }

            return nearestMapObject;
        }
    }
}
