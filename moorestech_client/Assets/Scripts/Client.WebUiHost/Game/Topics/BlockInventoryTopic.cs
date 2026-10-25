using System;
using Client.Game.InGame.Train.Timetable;
using Client.Game.InGame.Train.Unit;
using Client.Game.InGame.BeltTransport;
using Client.Game.InGame.FluidNetwork;
using Client.Game.InGame.Block;
using Client.Game.InGame.Context;
using Client.Game.InGame.UI.Inventory;
using Client.Game.InGame.UI.UIState;
using Client.Game.InGame.UI.UIState.State;
using Client.Game.InGame.UI.UIState.State.SubInventory;
using Client.WebUiHost.Boot;
using Client.WebUiHost.Common;
using Client.Game.InGame.UI.BlockDetail;
using Client.WebUiHost.Game.Topics.Inventory;
using Cysharp.Threading.Tasks;
using UniRx;

namespace Client.WebUiHost.Game.Topics
{
    public class BlockInventoryTopic : ITopicHandler, IDisposable
    {
        public const string TopicName = "block_inventory.current";
        private readonly WebSocketHub _hub;
        private readonly UIStateControl _uiStateControl;
        private readonly SubInventoryState _subInventoryState;
        private readonly OpenBlockDetailTracker _tracker;
        private readonly BlockNetworkInfoCache _networkCache;
        private readonly TrainUnitClientCache _trainUnitClientCache;
        private readonly IClientTrainTimetableLookup _timetables;
        private readonly TrainTimetableFetcher _timetableFetcher;
        private readonly BeltTransportClientCache _beltCache;
        private readonly FluidNetworkClientCache _fluidCache;
        private readonly CompositeDisposable _subscriptions = new();
        private readonly BlockInventoryRepublishPolicy _republishPolicy;
        private bool _publishScheduled;
        private bool _continuousSamplePending;
        private bool _disposed;

        public BlockInventoryTopic(WebSocketHub hub, UIStateControl uiStateControl, SubInventoryState subInventoryState, OpenBlockDetailTracker tracker, BlockNetworkInfoCache networkCache, TrainUnitClientCache trainUnitClientCache, IClientTrainTimetableLookup timetables, TrainTimetableFetcher timetableFetcher, OpenTrainTimetableTracker openTrainTimetableTracker, BeltTransportClientCache beltCache, FluidNetworkClientCache fluidCache)
        {
            _hub = hub;
            _uiStateControl = uiStateControl;
            _subInventoryState = subInventoryState;
            _tracker = tracker;
            _networkCache = networkCache;
            _trainUnitClientCache = trainUnitClientCache;
            _timetables = timetables;
            _timetableFetcher = timetableFetcher;
            _beltCache = beltCache;
            _fluidCache = fluidCache;
            _republishPolicy = new BlockInventoryRepublishPolicy(tracker, fluidCache);

            // 追跡ブロックの張り替えで再配信の方針を導き直す（購読前に既に追跡中なら今の分も）
            // Re-derive the republish policy when the tracked block changes (and for one already tracked before subscribing)
            _republishPolicy.SetTrackedBlock(tracker.TrackedBlock);
            tracker.OnTrackedBlockChanged.Subscribe(_republishPolicy.SetTrackedBlock).AddTo(_subscriptions);
            tracker.OnTrackedBlockStateChanged.Subscribe(_ => OnTrackedBlockStateChanged()).AddTo(_subscriptions);
            // 搬出統計の5tickブロックが完成するたびに、開いているベルトの詳細を出し直す
            // Republish the open belt's detail each time a 5-tick block of the exit statistics completes
            beltCache.ExitStatistics.OnWindowAdvanced.Subscribe(_ =>
            {
                if (_republishPolicy.RepublishOnBeltWindow) SchedulePublish();
            }).AddTo(_subscriptions);
            // 時刻表更新・取得失敗・所属列車の変化で再配信する
            // Republish on the open train's timetable updates, fetch failures, and owning-train changes
            openTrainTimetableTracker.OnRepublishRequested.Subscribe(_ => SchedulePublish()).AddTo(_subscriptions);
            // 開閉（UIステート遷移）とスロット更新の両方を購読して push する
            // Subscribe to open/close (UI-state transitions) and slot updates, then push
            uiStateControl.OnStateChanged.Subscribe(_ => SchedulePublish()).AddTo(_subscriptions);
            subInventoryState.OnSubInventoryUpdated.Subscribe(_ => SchedulePublish()).AddTo(_subscriptions);
            // ネットワーク取得完了でも再配信する
            // Republish when a network fetch completes
            networkCache.OnUpdated.Subscribe(_ => SchedulePublish()).AddTo(_subscriptions);
            // ギア連続値とパイプの中身はWS帯域のため4フレーム間隔に間引いて配信する（tickでなくフレーム。判断記録 D7）
            // Thin gear continuous values and pipe contents to every four frames for WS bandwidth (frames, not ticks; decision D7)
            Observable.IntervalFrame(4)
                .Where(_ => _continuousSamplePending || _republishPolicy.HasPipeContentsChanged())
                .Subscribe(_ => PublishContinuousSample())
                .AddTo(_subscriptions);
        }

        public UniTask<string> GetSnapshotJsonAsync()
        {
            return UniTask.FromResult(BuildJson());
        }

        public void Dispose()
        {
            _disposed = true;
            _subscriptions.Dispose();
        }

        private void OnTrackedBlockStateChanged()
        {
            if (_republishPolicy.SampleContinuously) _continuousSamplePending = true;
            else SchedulePublish();
        }

        private string BuildJson()
        {
            // SubInventory 状態かつ発生元がブロックのときだけ開いている扱い（列車等の非ブロックは閉）
            // Open only in the SubInventory state with a block source (non-block sources like trains are closed)
            var open = _uiStateControl.CurrentState == UIStateEnum.SubInventory;
            var sub = _subInventoryState.CurrentSubInventory;
            if (!open || sub == null) return WebUiJson.Serialize(new BlockInventoryDto { Open = false });

            // 列車も既存の統一SubInventoryを同じwireへ写し、並行インベントリ経路を作らない
            // Map trains from the unified SubInventory onto the same wire instead of creating a parallel inventory path.
            if (_subInventoryState.CurrentSubInventorySource is TrainSubInventorySource trainSource)
            {
                return WebUiJson.Serialize(TrainInventoryDtoFactory.Create(trainSource, sub, _trainUnitClientCache, _timetables, _timetableFetcher, ClientDIContext.BlockGameObjectDataStore));
            }

            // 開いているブロックの解決はtrackerが遷移・スロット更新のたびに済ませている。ここは読むだけにする
            // The tracker already resolves the open block on every transition and slot update; only read it here
            var block = _tracker.TrackedBlock;
            if (block == null || _subInventoryState.CurrentSubInventorySource is not BlockSubInventorySource blockSource || !IsStillPlaced(blockSource, block))
            {
                return WebUiJson.Serialize(new BlockInventoryDto { Open = false });
            }
            _republishPolicy.CapturePipeBaselineIfPending(block);
            var dto = BlockInventoryDtoFactory.Create(blockSource, sub, block, _networkCache, _beltCache, _fluidCache);
            return WebUiJson.Serialize(dto);

            #region Internal

            // 撤去されたフレームから閉じる遷移（次のUpdate）までの間は、移す前と同じく閉として出す（撤去済みのGameObjectを読まない）
            // Between a removal and the closing transition on the next Update, publish closed as before (never read a removed GameObject)
            bool IsStillPlaced(BlockSubInventorySource source, BlockGameObject tracked)
            {
                return ClientDIContext.BlockGameObjectDataStore.TryGetBlockGameObject(source.BlockPosition, out var placed) && ReferenceEquals(placed, tracked);
            }

            #endregion
        }
        private void PublishContinuousSample()
        {
            _continuousSamplePending = false;
            SchedulePublish();
        }
        // INFRA-7 デバウンス規約: 中間状態を畳んでフレーム末に一度だけ全量配信する
        // INFRA-7 debounce rule: fold intermediate states and publish once at end of frame
        private void SchedulePublish()
        {
            if (_publishScheduled) return; // silent-ok: 同フレームの再配信を畳む / 予約済み配信が最新DTOを届ける
            _publishScheduled = true;
            PublishAtEndOfFrame().Forget();
            #region Internal
            async UniTaskVoid PublishAtEndOfFrame()
            {
                await UniTask.Yield(PlayerLoopTiming.PostLateUpdate);
                _publishScheduled = false;
                if (_disposed) return; // silent-ok: 終了済みホストへ送信しない / ホスト停止処理が所有する正常終了
                _hub.Publish(TopicName, BuildJson());
            }
            #endregion
        }
    }
}
