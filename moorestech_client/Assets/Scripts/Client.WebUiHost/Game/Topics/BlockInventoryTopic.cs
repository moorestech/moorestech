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
using Client.WebUiHost.Game.Topics.BlockDetail;
using Core.Master;
using Cysharp.Threading.Tasks;
using Mooresmaster.Model.BlocksModule;
using Server.Event.EventReceive;
using UniRx;
namespace Client.WebUiHost.Game.Topics
{
    /// <summary>
    /// block_inventory.current トピック: 現在開いているサブインベントリ（ブロック）の中身を push
    /// block_inventory.current topic: pushes the contents of the currently open sub-inventory (block)
    /// </summary>
    public class BlockInventoryTopic : ITopicHandler, IDisposable
    {
        public const string TopicName = "block_inventory.current";
        private readonly TrainUnitClientCache _trainUnitClientCache;
        private readonly BeltTransportClientCache _beltCache;
        private readonly FluidNetworkClientCache _fluidCache;
        private readonly IDisposable _beltWindowSubscription;
        private bool _republishOnBeltWindow;
        // パイプの中身はブロック状態イベントで届かずクライアント側のコピーが毎tick進めるので、連続値と同じ間隔で見て、量か流体の種類が前回の配信から変わった回だけ出し直す
        // Pipe contents no longer arrive as block state events but advance in the client-side copy every tick, so they are sampled at the continuous-sample interval and republished only when the amount or fluid id changed since the last publish
        private bool _republishOnFluidReplica;
        private long _publishedPipeAmount;
        private FluidId _publishedPipeFluidId;
        private readonly IClientTrainTimetableLookup _timetables;
        private readonly TrainTimetableFetcher _timetableFetcher;
        private readonly OpenTrainTimetableTracker _openTrainTimetableTracker;
        private readonly IDisposable _trainTimetableSubscription;
        private readonly WebSocketHub _hub;
        private readonly UIStateControl _uiStateControl;
        private readonly SubInventoryState _subInventoryState;
        private readonly BlockNetworkInfoCache _networkCache = new();
        private readonly IDisposable _subInventorySubscription;
        private readonly IDisposable _continuousSampleSubscription;
        private readonly IDisposable _networkCacheSubscription;
        private BlockGameObject _trackedBlock;
        private IDisposable _blockStateSubscription;
        private bool _publishScheduled;
        private bool _continuousSamplePending;
        private bool _sampleContinuously;
        private bool _disposed;
        // Task 8 の action handler がスナップショット反映に使う公開口
        // Public access point used by Task 8 action handlers to apply snapshots
        public BlockNetworkInfoCache NetworkCache => _networkCache;
        public BlockInventoryTopic(WebSocketHub hub, UIStateControl uiStateControl, SubInventoryState subInventoryState, TrainUnitClientCache trainUnitClientCache, IClientTrainTimetableLookup timetables, TrainTimetableFetcher timetableFetcher, BeltTransportClientCache beltCache, FluidNetworkClientCache fluidCache)
        {
            _trainUnitClientCache = trainUnitClientCache;
            _beltCache = beltCache;
            _fluidCache = fluidCache;
            // 搬出統計の5tickブロックが完成するたびに、開いているベルトの詳細を出し直す
            // Republish the open belt's detail each time a 5-tick block of the exit statistics completes
            _beltWindowSubscription = beltCache.ExitStatistics.OnWindowAdvanced.Subscribe(_ =>
            {
                if (_republishOnBeltWindow) SchedulePublish();
            });
            _timetables = timetables;
            _timetableFetcher = timetableFetcher;
            // 時刻表更新・取得失敗・所属列車の変化で再配信する
            // Republish on the open train's timetable updates, fetch failures, and owning-train changes
            _openTrainTimetableTracker = new OpenTrainTimetableTracker(subInventoryState, uiStateControl, trainUnitClientCache, timetables, timetableFetcher);
            _trainTimetableSubscription = _openTrainTimetableTracker.OnRepublishRequested.Subscribe(_ => SchedulePublish());
            _hub = hub;
            _uiStateControl = uiStateControl;
            _subInventoryState = subInventoryState;
            // 開閉（UIステート遷移）とスロット更新の両方を購読して push する
            // Subscribe to open/close (UI-state transitions) and slot updates, then push
            _uiStateControl.OnStateChanged += OnStateChanged;
            _subInventorySubscription = _subInventoryState.OnSubInventoryUpdated.Subscribe(_ => SchedulePublish());
            // ネットワーク取得完了でも再配信する
            // Republish when a network fetch completes
            _networkCacheSubscription = _networkCache.OnUpdated.Subscribe(_ => SchedulePublish());
            // ギア連続値を4tick間隔で配信する
            // Sample RPM/torque and other continuous values every four ticks, publishing only the latest state
            _continuousSampleSubscription = Observable.IntervalFrame(4)
                .Where(_ => _continuousSamplePending || HasPipeContentsChanged())
                .Subscribe(_ => PublishContinuousSample());
        }
        public UniTask<string> GetSnapshotJsonAsync()
        {
            return UniTask.FromResult(BuildJson());
        }
        public void Dispose()
        {
            _disposed = true;
            _uiStateControl.OnStateChanged -= OnStateChanged;
            _subInventorySubscription.Dispose();
            _continuousSampleSubscription.Dispose();
            _beltWindowSubscription.Dispose();
            _trainTimetableSubscription.Dispose();
            _openTrainTimetableTracker.Dispose();
            _networkCacheSubscription.Dispose();
            TrackBlock(null);
        }
        private void OnStateChanged(UIStateEnum state)
        {
            SchedulePublish();
        }
        private void PublishContinuousSample()
        {
            _continuousSamplePending = false;
            SchedulePublish();
        }
        // 開いているパイプの量・流体の種類をクライアント側のコピーから読み、前回の配信と違えば控えて true を返す(変わらない間は送らない)
        // Read the open pipe's amount and fluid id from the client-side copy; when they differ from the last publish, remember them and return true (nothing is sent while unchanged)
        private bool HasPipeContentsChanged()
        {
            if (!_republishOnFluidReplica || !_fluidCache.TryGetNodeIndex(_trackedBlock.BlockPosInfo.OriginalPos, out var pipeNode)) return false;
            var network = _fluidCache.Replica.Network;
            var amount = network.NodeAmounts[pipeNode];
            var fluidId = network.NodeFluidIds[pipeNode];
            if (amount == _publishedPipeAmount && fluidId == _publishedPipeFluidId) return false;
            _publishedPipeAmount = amount;
            _publishedPipeFluidId = fluidId;
            return true;
        }
        private void OnTrackedBlockStateChanged()
        {
            if (_sampleContinuously) _continuousSamplePending = true;
            else SchedulePublish();
        }
        // INFRA-7 デバウンス規約: 中間状態を畳んでフレーム末に一度だけ全量配信する
        // INFRA-7 debounce rule: fold intermediate states and publish once at end of frame
        private void SchedulePublish()
        {
            if (_publishScheduled) return;
            _publishScheduled = true;
            PublishAtEndOfFrame().Forget();
            #region Internal
            async UniTaskVoid PublishAtEndOfFrame()
            {
                await UniTask.Yield(PlayerLoopTiming.PostLateUpdate);
                _publishScheduled = false;
                if (_disposed) return;
                _hub.Publish(TopicName, BuildJson());
            }
            #endregion
        }
        private string BuildJson()
        {
            // SubInventory 状態かつ発生元がブロックのときだけ開いている扱い（列車等の非ブロックは閉）
            // Open only in the SubInventory state with a block source (non-block sources like trains are closed)
            var open = _uiStateControl.CurrentState == UIStateEnum.SubInventory;
            var sub = _subInventoryState.CurrentSubInventory;
            var blockSource = _subInventoryState.CurrentSubInventorySource as BlockSubInventorySource;
            if (!open || sub == null)
            {
                TrackBlock(null);
                return WebUiJson.Serialize(new BlockInventoryDto { Open = false });
            }
            // 列車も既存の統一SubInventoryを同じwireへ写し、並行インベントリ経路を作らない
            // Map trains from the unified SubInventory onto the same wire instead of creating a parallel inventory path.
            if (_subInventoryState.CurrentSubInventorySource is TrainSubInventorySource trainSource)
            {
                TrackBlock(null);
                return WebUiJson.Serialize(TrainInventoryDtoFactory.Create(trainSource, sub, _trainUnitClientCache, _timetables, _timetableFetcher, ClientDIContext.BlockGameObjectDataStore));
            }
            if (blockSource == null)
            {
                TrackBlock(null);
                return WebUiJson.Serialize(new BlockInventoryDto { Open = false });
            }
            // BlockGameObject は既存公開の datastore から座標で解決する（uGUI 編集なし）
            // Resolve the BlockGameObject by position via the existing public datastore (no uGUI edits)
            if (!ClientDIContext.BlockGameObjectDataStore.TryGetBlockGameObject(blockSource.BlockPosition, out var block))
            {
                TrackBlock(null);
                return WebUiJson.Serialize(new BlockInventoryDto { Open = false });
            }
            TrackBlock(block);
            var dto = BlockInventoryDtoFactory.Create(blockSource, sub, block, _networkCache, _beltCache, _fluidCache);
            return WebUiJson.Serialize(dto);
        }
        // 追跡ブロックを切り替え、state イベント購読とネットワーク取得を張り替える
        // Switch the tracked block, re-wiring the state-event subscription and network fetches
        private void TrackBlock(BlockGameObject block)
        {
            if (ReferenceEquals(_trackedBlock, block)) return;
            // 前のブロックの state イベント購読を解除する
            // Unsubscribe the previous block's state event
            _blockStateSubscription?.Dispose();
            _blockStateSubscription = null;
            _trackedBlock = block;
            if (block == null)
            {
                _sampleContinuously = false;
                _republishOnBeltWindow = false;
                _republishOnFluidReplica = false;
                _networkCache.Clear();
                return;
            }
            // 開いた時点でコピーに載っていないパイプは、載るまで中身を出さない(調べ直さない)。載っていれば今の中身を配信済みとして控え、最初の配信は通常の開く経路に任せる
            // A pipe not yet in the copy when opened shows no contents until reopened (no re-check); when present, the current contents count as published and the first publish goes through the normal open path
            _republishOnFluidReplica = _fluidCache.TryGetNodeIndex(block.BlockPosInfo.OriginalPos, out var pipeNode);
            if (_republishOnFluidReplica)
            {
                _publishedPipeAmount = _fluidCache.Replica.Network.NodeAmounts[pipeNode];
                _publishedPipeFluidId = _fluidCache.Replica.Network.NodeFluidIds[pipeNode];
            }
            // uGUI BlockGameObject と同じ per-block イベントタグを直接購読して再配信トリガにする
            // Directly subscribe the same per-block event tag as uGUI BlockGameObject as the republish trigger
            var eventTag = ChangeBlockStateEventPacket.CreateSpecifiedBlockEventTag(block.BlockPosInfo);
            _blockStateSubscription = ClientContext.VanillaApi.Event.SubscribeEventResponse(
                eventTag,
                _ => OnTrackedBlockStateChanged());
            // どの集約情報を追うかはキャッシュが param から決め、歯車網の追跡有無もキャッシュから読む
            // The cache decides from the param which aggregates to follow, and whether gear is tracked is read back from it
            _networkCache.Track(block);
            _sampleContinuously = _networkCache.IsTrackingGear;
            _republishOnBeltWindow = block.BlockMasterElement.BlockParam is IBeltSpeedParam;
        }
    }
}
