using System;
using System.Collections.Generic;
using System.Linq;
using Client.Common.Asset;
using Client.Game.Common;
using Client.Game.InGame.Context;
using Client.Network.API;
using CommandForgeGenerator.Command;
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
    public class MapObjectGameObjectDatastore : MonoBehaviour, IInitialEventApplyWaitTarget, ISkitWorldObjectControl
    {
        // 2011個規模の起動スパイクを避けるためこの個数ごとにフレームを跨ぐ
        // Cross a frame every this many objects to avoid a startup spike at the ~2011-object scale
        private const int FrameYieldObjectInterval = 100;

        private readonly Dictionary<int, MapObjectGameObject> _allMapObjects = new();
        private readonly Dictionary<Guid, GameObject> _prefabCacheByMapObjectGuid = new();
        private readonly MapObjectNearestSearcher _nearestSearcher = new();

        // 生成ループの完了と例外を初期化パイプラインがawaitできる形で保持する
        // Retain the instantiation loop's completion and exceptions for the initialization pipeline to await
        private UniTask? _initialApplyTask;

        public UniTask WaitForInitialApplyAsync()
        {
            // 開始前の待機要求は順序バグ。既定値タスク（完了扱い）で素通りさせず失敗させる
            // Waiting before the start is an ordering bug; never let the default (completed) task slip through
            if (_initialApplyTask == null)
                throw new InvalidOperationException("[MapObjectGameObjectDatastore] Construct前に待機が要求されました");
            return _initialApplyTask.Value;
        }

        [Inject]
        public void Construct(InitialHandshakeResponse handshakeResponse)
        {
            // イベント購読は同期で確定させ、生成本体はフレーム分散の保持タスクへ委譲する
            // Subscribe synchronously, then delegate the instantiation itself to a frame-distributed retained task
            ClientContext.VanillaApi.Event.SubscribeEventResponse(MapObjectUpdateEventPacket.EventTag, OnUpdateMapObject);
            _initialApplyTask = InstantiateMapObjectsFromLayoutAsync().Preserve();

            #region Internal

            async UniTask InstantiateMapObjectsFromLayoutAsync()
            {
                // 破壊/HPの初期状態はva:mapObjectInfoスナップショットをinstanceIdで引く（Layoutと同一集合が前提）
                // Initial destroy/HP state comes from the va:mapObjectInfo snapshot keyed by instanceId (same set as the layout)
                var snapshotByInstanceId = handshakeResponse.MapObjects.ToDictionary(info => info.InstanceId);
                var cancellationToken = this.GetCancellationTokenOnDestroy();

                var processedCount = 0;
                foreach (var layout in handshakeResponse.MapLayout.MapObjects)
                {
                    // guidは正常データ前提でparseする（不正guidはT8のデータ修正対象・ここでの防御は過剰）
                    // Parse guid assuming valid data (malformed guids are a T8 data fix; defending here is overkill)
                    var mapObjectGuid = new Guid(layout.MapObjectGuid);

                    // master欠落・load失敗はResolvePrefabOrNull内でLogError済み。個体だけskipし残りは生成しきる
                    // Master-missing or load-failure is already logged inside; skip just this one and keep generating the rest
                    var prefab = ResolvePrefabOrNull(mapObjectGuid);
                    if (prefab == null) continue;

                    // スナップショット欠落はInstantiate前に検出し、orphan instanceを作らずskipする
                    // Detect a missing snapshot before Instantiate so no orphan instance is created, then skip
                    if (!snapshotByInstanceId.TryGetValue(layout.InstanceId, out var snapshot))
                    {
                        Debug.LogError($"MapObject snapshot missing. InstanceId:{layout.InstanceId} MapObjectGuid:{mapObjectGuid}");
                        continue;
                    }

                    // 生成時のRotation/Scaleを実インスタンスへ戻す。既定値のままだと全個体が同じ向きで直立し裸地も生成時サイズで広がる
                    // Restore the generated rotation and scale; the defaults face every instance alike and spread bare ground at the generated size
                    var rotation = new Quaternion(layout.RotationX, layout.RotationY, layout.RotationZ, layout.RotationW);
                    var instance = Instantiate(prefab, new Vector3(layout.X, layout.Y, layout.Z), rotation, transform);
                    instance.transform.localScale = new Vector3(layout.ScaleX, layout.ScaleY, layout.ScaleZ);

                    // rootにMapObjectGameObjectが無いのはprefab authoring不正。生成物を破棄してskipする
                    // Missing MapObjectGameObject on root is invalid prefab authoring; destroy the instance and skip
                    var mapObject = instance.GetComponent<MapObjectGameObject>();
                    if (mapObject == null)
                    {
                        Debug.LogError($"MapObject prefab has no MapObjectGameObject on root. MapObjectGuid:{mapObjectGuid}");
                        Destroy(instance);
                        continue;
                    }

                    // instanceId重複はTryAddで検出し、重複個体を破棄してskipする（Addのthrowは起動ハングを招くため不可）
                    // Detect duplicate instanceId via TryAdd; destroy the duplicate and skip (Add's throw would hang startup)
                    mapObject.SetRuntimeIdentity(layout.InstanceId, layout.MapObjectGuid);
                    if (!_allMapObjects.TryAdd(layout.InstanceId, mapObject))
                    {
                        Debug.LogError($"MapObject duplicate InstanceId:{layout.InstanceId} MapObjectGuid:{mapObjectGuid}");
                        Destroy(instance);
                        continue;
                    }

                    // 登録後にスナップショットで初期状態（破壊/HP）を適用する
                    // Apply the initial state (destroy/HP) from the snapshot after registration
                    mapObject.Initialize(snapshot);

                    // 最寄り探索の候補へ登録する（初期破壊済みは探索時の生存フィルタで除かれる）
                    // Register as a nearest-search candidate (ones destroyed in the snapshot drop out at the live filter on search)
                    _nearestSearcher.Register(mapObject);

                    // フレーム分散でInstantiateし、起動時の描画スパイクを抑える
                    // Instantiate spread across frames to suppress the render spike at startup
                    processedCount++;
                    if (processedCount % FrameYieldObjectInterval == 0) await UniTask.Yield(cancellationToken);
                }
            }

            GameObject ResolvePrefabOrNull(Guid mapObjectGuid)
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
                    // 破壊は索引へ即時反映せず、次の探索で該当guidだけ再構築する
                    // Destruction isn't applied to the index immediately; the next search rebuilds just this guid
                    _nearestSearcher.MarkDirty(mapObject.MapObjectGuid);
                    break;
                case MapObjectUpdateEventMessagePack.HpUpdateEventType:
                    mapObject.UpdateHp(data.CurrentHp);
                    break;
                default:
                    throw new Exception("MapObjectUpdateEventProtocol: EventTypeが不正か実装されていません");
            }
        }

        public void SetActive(bool enable)
        {
            gameObject.SetActive(enable);
        }

        public MapObjectGameObject SearchNearestMapObject(Guid mapObjectGuid, Vector3 position)
        {
            return _nearestSearcher.SearchNearest(mapObjectGuid, position);
        }
    }
}
