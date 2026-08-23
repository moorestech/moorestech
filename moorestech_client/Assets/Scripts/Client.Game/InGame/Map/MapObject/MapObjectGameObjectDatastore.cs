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
        // 起動待機を解除する近傍の半径。残りはゲーム開始後に距離順で後着生成する（ADR 0030）
        // Radius of the near field that releases the startup wait; the rest streams in by distance after the game starts (ADR 0030)
        private const float NearFieldRadius = 150f;

        // ローディング中（近傍）とゲーム開始後（後着）のフレームあたり生成時間予算
        // Per-frame instantiation time budgets while loading (near field) and after the game starts (background)
        private const double NearFieldFrameBudgetMilliseconds = 16.0;
        private const double BackgroundFrameBudgetMilliseconds = 4.0;

        private readonly Dictionary<int, MapObjectGameObject> _allMapObjects = new();
        private readonly MapObjectNearestSearcher _nearestSearcher = new();
        private readonly MapObjectPendingStateLedger _pendingStateLedger = new();

        // 近傍完了（起動待機の解除点）と全量完了を別々にawaitできる形で保持する
        // Retain near-field completion (the startup wait release) and full completion as separately awaitable tasks
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
            // 全量完了の正規待機API。全個体前提の検証・テストはこちらを待つ（ADR 0030）
            // The official wait for full instantiation; checks and tests that assume every object await this (ADR 0030)
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

            // 全layoutを一度だけPlayerPosからの距離順に並べ、近傍→遠方の順で生成する（ADR 0030）
            // Sort every layout once by distance from PlayerPos and instantiate near to far (ADR 0030)
            var sortedEntries = MapObjectLayoutDistanceOrder.Sort(handshakeResponse.MapLayout.MapObjects, handshakeResponse.PlayerPos);
            var nearFieldCount = MapObjectLayoutDistanceOrder.CountWithinRadius(sortedEntries, NearFieldRadius);

            _initialApplyTask = InstantiateRangeAsync(0, nearFieldCount, NearFieldFrameBudgetMilliseconds).Preserve();
            _allInstantiatedTask = InstantiateBackgroundAsync().Preserve();

            // 後着の失敗を誰もawaitしない起動経路でもConsoleへ出す
            // Surface background failures in the Console even when nothing on the startup path awaits them
            _allInstantiatedTask.Value.Forget();

            #region Internal

            async UniTask InstantiateBackgroundAsync()
            {
                // 近傍の完了（と失敗）を引き継いでから残り全量を後着させる
                // Take over near-field completion (and failure) before streaming in the remainder
                await _initialApplyTask.Value;
                await InstantiateRangeAsync(nearFieldCount, sortedEntries.Count, BackgroundFrameBudgetMilliseconds);
            }

            async UniTask InstantiateRangeAsync(int startIndex, int endIndexExclusive, double frameBudgetMilliseconds)
            {
                // 時間予算を使い切るまで同一フレームで生成し続け、超えたらフレームを跨ぐ（ADR 0030）
                // Keep instantiating within the frame until the time budget runs out, then cross a frame (ADR 0030)
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

            // 未生成宛は捨てず台帳へ保留し、後着生成時にスナップショットより優先して適用する（ADR 0030）
            // Events for not-yet-instantiated objects are held in the ledger and override the snapshot at late instantiation (ADR 0030)
            if (!_allMapObjects.TryGetValue(data.InstanceId, out var mapObject))
            {
                switch (data.EventType)
                {
                    case MapObjectUpdateEventMessagePack.DestroyEventType:
                        _pendingStateLedger.RecordDestroy(data.InstanceId);
                        break;
                    case MapObjectUpdateEventMessagePack.HpUpdateEventType:
                        _pendingStateLedger.RecordHp(data.InstanceId, data.CurrentHp);
                        break;
                    default:
                        throw new Exception("MapObjectUpdateEventProtocol: EventTypeが不正か実装されていません");
                }

                return;
            }

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
