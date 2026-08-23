using System;
using System.Collections.Generic;
using System.Threading;
using Client.Common.Asset;
using Core.Master;
using Cysharp.Threading.Tasks;
using Server.Protocol.PacketResponse;
using Server.Protocol.PacketResponse.MapData;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Client.Game.InGame.Map.MapObject
{
    /// <summary>
    ///     layoutから個体生成しスナップショット適用・登録簿への登録を行う
    ///     Instantiates a map object from a layout, applies its snapshot, and hands it to the registry
    /// </summary>
    public sealed class MapObjectLayoutInstantiator
    {
        private readonly Transform _parent;
        private readonly MapObjectRegistry _registry;
        private readonly Dictionary<int, GetMapObjectInfoProtocol.MapObjectsInfoMessagePack> _snapshotByInstanceId;
        private readonly Dictionary<Guid, GameObject> _prefabCacheByMapObjectGuid = new();

        public MapObjectLayoutInstantiator(
            Transform parent,
            MapObjectRegistry registry,
            Dictionary<int, GetMapObjectInfoProtocol.MapObjectsInfoMessagePack> snapshotByInstanceId)
        {
            _parent = parent;
            _registry = registry;
            _snapshotByInstanceId = snapshotByInstanceId;
        }

        public async UniTask InstantiateFromLayoutAsync(MapObjectLayoutMessagePack layout, CancellationToken cancellationToken)
        {
            // guidは正常データ前提でparseする（不正guidはT8のデータ修正対象・ここでの防御は過剰）
            // Parse guid assuming valid data (malformed guids are a T8 data fix; defending here is overkill)
            var mapObjectGuid = new Guid(layout.MapObjectGuid);

            // 失敗個体はskipし続行
            // Skip this instance on failure and keep going
            var prefab = await ResolvePrefabOrNullAsync(mapObjectGuid);
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

            // 登録簿へ渡す前に初期状態を当てる。保留イベントの優先適用は登録簿側の不変条件が担う（ADR 0030）
            // Apply the initial state before handing it over; overriding it with pending events is the registry's own invariant (ADR 0030)
            mapObject.SetRuntimeIdentity(layout.InstanceId, layout.MapObjectGuid);
            mapObject.Initialize(snapshot);

            // instanceId重複は登録簿が検出する。重複個体を破棄してskipする（例外は起動ハングを招くため不可）
            // The registry detects duplicate instanceIds; destroy the duplicate and skip (throwing would hang startup)
            if (!_registry.TryRegister(mapObject))
            {
                Debug.LogError($"MapObject duplicate InstanceId:{layout.InstanceId} MapObjectGuid:{mapObjectGuid}");
                Object.Destroy(instance);
            }

            #region Internal

            async UniTask<GameObject> ResolvePrefabOrNullAsync(Guid guid)
            {
                // 失敗もnullとしてキャッシュする。同一guidが千個規模で並ぶためloadとLogErrorはguidごと1回に抑える
                // Failures are cached as null too; a guid can repeat by the thousand so keep the load and LogError once per guid
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

                // 同期loadはWaitForCompletionでメインスレッドを止め、後着中に数十〜数百msのヒッチを出すので非同期で待つ
                // The sync load blocks the main thread via WaitForCompletion and would stutter background streaming, so await instead
                var loaded = await AddressableLoader.LoadAsyncDefault<GameObject>(element.AddressablePath, cancellationToken);
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
