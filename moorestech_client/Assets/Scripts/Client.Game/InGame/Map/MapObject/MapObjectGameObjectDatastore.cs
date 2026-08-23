using System;
using System.Linq;
using Client.Game.Common;
using Client.Game.InGame.Context;
using Client.Network.API;
using CommandForgeGenerator.Command;
using Cysharp.Threading.Tasks;
using MessagePack;
using Server.Event.EventReceive;
using UniRx;
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

        private readonly MapObjectRegistry _registry = new();

        // スキットが世界オブジェクトを消している間は生成を止める。非活性下で生成するとrayTargetのInitializeが空振りし恒久的に壊れる
        // Instantiation stops while a skit hides world objects; instantiating under an inactive parent leaves rayTargets uninitialized forever
        private readonly ReactiveProperty<bool> _isWorldObjectActive = new(true);

        // 後着を含む全量の生成完了。未完了中の探索空振りは「まだ生成されていない」であって欠落ではない
        // Full instantiation including the background stream; a miss while this is false means "not yet", not "missing"
        private readonly ReactiveProperty<bool> _isAllInstantiated = new(false);

        public IReadOnlyReactiveProperty<bool> IsAllInstantiated => _isAllInstantiated;

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
            // 全量待機の正規API（テスト用）。ゲーム開始前でも待てる完了ソースとして保持する
            // The official wait for full instantiation (used by tests); it stays awaitable even before the game starts
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
            var instantiator = new MapObjectLayoutInstantiator(transform, _registry, snapshotByInstanceId);
            var cancellationToken = this.GetCancellationTokenOnDestroy();

            // 距離順に並べ近傍境界まで一度に確定させる
            // Order by distance and settle the near-field boundary in one call
            var nearFieldOrder = MapObjectLayoutDistanceOrder.SortNearFieldFirst(handshakeResponse.MapLayout.MapObjects, handshakeResponse.PlayerPos);

            _initialApplyTask = InstantiateRangeAsync(0, nearFieldOrder.NearFieldCount, NearFieldFrameBudgetMilliseconds).Preserve();
            _allInstantiatedTask = InstantiateBackgroundAsync().Preserve();

            #region Internal

            async UniTask InstantiateBackgroundAsync()
            {
                // 近傍完了後、さらにゲーム開始まで待つ。露頭生成・チュートリアル適用の最中に予算を奪わない（ADR 0030 R2）
                // Wait for the near field and then for the game to start, so the background stream never steals budget during startup (ADR 0030 R2)
                await _initialApplyTask.Value;
                await GameInitializedEvent.OnGameInitialized.Take(1).ToUniTask(cancellationToken: cancellationToken);

                // 後着レンジだけを観測対象にし、近傍の例外はWaitForInitialApplyAsync側の1系統のみが報告する
                // Scope the Forget() observer to the background range only, so near-field failures are reported once via WaitForInitialApplyAsync
                var backgroundTask = InstantiateRangeAsync(nearFieldOrder.NearFieldCount, nearFieldOrder.Entries.Count, BackgroundFrameBudgetMilliseconds).Preserve();
                backgroundTask.Forget();
                await backgroundTask;

                _isAllInstantiated.Value = true;
            }

            async UniTask InstantiateRangeAsync(int startIndex, int endIndexExclusive, double frameBudgetMilliseconds)
            {
                // 予算内は同一フレームで生成継続
                // Keep instantiating within the frame while budget remains
                var budget = new FrameTimeBudget(frameBudgetMilliseconds);
                for (var index = startIndex; index < endIndexExclusive; index++)
                {
                    // 活性復帰は購読で待つ。待っている間に経過時間が積み上がるので復帰時に予算を仕切り直す
                    // Reactivation is awaited via subscription; elapsed time piles up while waiting, so the budget restarts on return
                    if (!_isWorldObjectActive.Value)
                    {
                        await _isWorldObjectActive.Where(static isActive => isActive).ToUniTask(true, cancellationToken);
                        budget.Restart();
                    }

                    await instantiator.InstantiateFromLayoutAsync(nearFieldOrder.Entries[index].Layout, cancellationToken);

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
            var isInstantiated = _registry.TryGet(data.InstanceId, out var mapObject);

            switch (data.EventType)
            {
                case MapObjectUpdateEventMessagePack.DestroyEventType:
                    if (!isInstantiated)
                    {
                        _registry.RecordPendingDestroy(data.InstanceId);
                        break;
                    }

                    // 破壊は次回探索時にguid単位で再構築
                    // Destruction rebuilds just this guid at the next search
                    mapObject.DestroyMapObject();
                    _registry.MarkDirty(mapObject.MapObjectGuid);
                    break;
                case MapObjectUpdateEventMessagePack.HpUpdateEventType:
                    if (isInstantiated) mapObject.UpdateHp(data.CurrentHp);
                    else _registry.RecordPendingHp(data.InstanceId, data.CurrentHp);
                    break;
                default:
                    throw new Exception("MapObjectUpdateEventProtocol: EventTypeが不正か実装されていません");
            }
        }

        public void SetActive(bool enable)
        {
            gameObject.SetActive(enable);

            // 生成ループが従属する活性状態。gameObjectの実状態と一致させてから通知する
            // The instantiation loop follows this active state, pushed only after the gameObject itself matches
            _isWorldObjectActive.Value = enable;
        }

        public MapObjectGameObject SearchNearestMapObject(Guid mapObjectGuid, Vector3 position)
        {
            return _registry.SearchNearest(mapObjectGuid, position);
        }
    }
}
