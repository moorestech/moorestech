using System;
using System.Collections.Generic;
using System.Linq;
using Client.Game.Common;
using Client.Game.InGame.Context;
using Client.Network.API;
using CommandForgeGenerator.Command;
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
        // 近傍/後着の生成時間予算
        // Per-frame instantiation time budgets: near field and background
        private const double NearFieldFrameBudgetMilliseconds = 16.0;
        private const double BackgroundFrameBudgetMilliseconds = 4.0;

        private readonly Dictionary<int, MapObjectGameObject> _allMapObjects = new();
        private readonly MapObjectNearestSearcher _nearestSearcher = new();
        private readonly MapObjectPendingStateLedger _pendingStateLedger = new();

        // 近傍/全量完了を別々に保持
        // Retain near-field and full completion separately
        private UniTask? _initialApplyTask;
        private UniTask? _allInstantiatedTask;

        public UniTask WaitForInitialApplyAsync()
        {
            // 開始前の待機要求は順序バグ。既定値タスク（完了扱い）で素通りさせず失敗させる
            // Waiting before the start is an ordering bug; never let the default (completed) task slip through
            if (_initialApplyTask == null)
                throw new InvalidOperationException("[MapObjectGameObjectDatastore] Construct前に待機が要求されました");
            return _initialApplyTask.Value;
        }

        public UniTask WaitForAllInstantiatedAsync()
        {
            // 全量待機の正規API（テスト用）
            // The official wait for full instantiation (used by tests)
            if (_allInstantiatedTask == null)
                throw new InvalidOperationException("[MapObjectGameObjectDatastore] Construct前に全量待機が要求されました");
            return _allInstantiatedTask.Value;
        }

        [Inject]
        public void Construct(InitialHandshakeResponse handshakeResponse)
        {
            // イベント購読は同期で確定させ、生成本体は近傍→後着の2段の保持タスクへ委譲する
            // Subscribe synchronously, then delegate instantiation to the two retained near-field → background tasks
            ClientContext.VanillaApi.Event.SubscribeEventResponse(MapObjectUpdateEventPacket.EventTag, OnUpdateMapObject);

            // 破壊/HPの初期状態はva:mapObjectInfoスナップショットをinstanceIdで引く（Layoutと同一集合が前提）
            // Initial destroy/HP state comes from the va:mapObjectInfo snapshot keyed by instanceId (same set as the layout)
            var snapshotByInstanceId = handshakeResponse.MapObjects.ToDictionary(info => info.InstanceId);
            var instantiator = new MapObjectLayoutInstantiator(transform, _allMapObjects, snapshotByInstanceId, _nearestSearcher, _pendingStateLedger);
            var cancellationToken = this.GetCancellationTokenOnDestroy();

            // 距離順に並べ近傍から生成
            // Sort by distance and instantiate near to far
            var sortedEntries = MapObjectLayoutDistanceOrder.Sort(handshakeResponse.MapLayout.MapObjects, handshakeResponse.PlayerPos);
            var nearFieldCount = MapObjectLayoutDistanceOrder.CountWithinRadius(sortedEntries, MapObjectLayoutDistanceOrder.NearFieldRadius);

            _initialApplyTask = InstantiateRangeAsync(0, nearFieldCount, NearFieldFrameBudgetMilliseconds).Preserve();
            _allInstantiatedTask = InstantiateBackgroundAsync().Preserve();

            #region Internal

            async UniTask InstantiateBackgroundAsync()
            {
                // 近傍完了後に残りを後着
                // Stream in the remainder after near field completes
                await _initialApplyTask.Value;

                // 後着レンジだけを観測対象にし、近傍の例外はWaitForInitialApplyAsync側の1系統のみが報告する
                // Scope the Forget() observer to the background range only, so near-field failures are reported once via WaitForInitialApplyAsync
                var backgroundTask = InstantiateRangeAsync(nearFieldCount, sortedEntries.Count, BackgroundFrameBudgetMilliseconds).Preserve();
                backgroundTask.Forget();
                await backgroundTask;
            }

            async UniTask InstantiateRangeAsync(int startIndex, int endIndexExclusive, double frameBudgetMilliseconds)
            {
                // 予算内は同一フレームで生成継続
                // Keep instantiating within the frame while budget remains
                var budget = new FrameTimeBudget(frameBudgetMilliseconds);
                for (var index = startIndex; index < endIndexExclusive; index++)
                {
                    instantiator.InstantiateFromLayout(sortedEntries[index].Layout);

                    if (!budget.IsExhausted) continue;
                    await UniTask.Yield(cancellationToken);
                    budget.Restart();
                }
            }

            #endregion
        }

        private void OnUpdateMapObject(byte[] payLoad)
        {
            var data = MessagePackSerializer.Deserialize<MapObjectUpdateEventMessagePack>(payLoad);

            // 未生成宛は台帳へ保留し生成時に優先適用
            // Not-yet-instantiated targets are held in the ledger and applied first at instantiation
            var isInstantiated = _allMapObjects.TryGetValue(data.InstanceId, out var mapObject);

            switch (data.EventType)
            {
                case MapObjectUpdateEventMessagePack.DestroyEventType:
                    if (isInstantiated)
                    {
                        mapObject.DestroyMapObject();
                        // 破壊は次回探索時にguid単位で再構築
                        // Destruction rebuilds just this guid at the next search
                        _nearestSearcher.MarkDirty(mapObject.MapObjectGuid);
                    }
                    else
                    {
                        _pendingStateLedger.RecordDestroy(data.InstanceId);
                    }

                    break;
                case MapObjectUpdateEventMessagePack.HpUpdateEventType:
                    if (isInstantiated)
                        mapObject.UpdateHp(data.CurrentHp);
                    else
                        _pendingStateLedger.RecordHp(data.InstanceId, data.CurrentHp);
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
