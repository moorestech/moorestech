using System;
using System.Collections.Generic;
using Client.Game.InGame.Block;
using Client.Game.InGame.Context;
using Client.Game.InGame.UI.Inventory;
using Client.Game.InGame.UI.UIState;
using Client.Game.InGame.UI.UIState.State;
using Client.Game.InGame.UI.UIState.State.SubInventory;
using Client.WebUiHost.Boot;
using Client.WebUiHost.Common;
using Client.WebUiHost.Game.Topics.BlockDetail;
using Cysharp.Threading.Tasks;
using Server.Event.EventReceive;

namespace Client.WebUiHost.Game.Topics
{
    /// <summary>
    /// block_inventory.current トピック: 現在開いているサブインベントリ（ブロック）の中身を push
    /// block_inventory.current topic: pushes the contents of the currently open sub-inventory (block)
    /// </summary>
    public class BlockInventoryTopic : ITopicHandler, IDisposable
    {
        public const string TopicName = "block_inventory.current";

        private readonly WebSocketHub _hub;
        private readonly UIStateControl _uiStateControl;
        private readonly SubInventoryState _subInventoryState;
        private readonly BlockNetworkInfoCache _networkCache = new();
        private BlockGameObject _trackedBlock;
        private IDisposable _blockStateSubscription;
        private bool _publishScheduled;
        private bool _disposed;

        // Task 8 の action handler がスナップショット反映に使う公開口
        // Public access point used by Task 8 action handlers to apply snapshots
        public BlockNetworkInfoCache NetworkCache => _networkCache;

        public BlockInventoryTopic(WebSocketHub hub, UIStateControl uiStateControl, SubInventoryState subInventoryState)
        {
            _hub = hub;
            _uiStateControl = uiStateControl;
            _subInventoryState = subInventoryState;

            // 開閉（UIステート遷移）とスロット更新の両方を購読して push する
            // Subscribe to open/close (UI-state transitions) and slot updates, then push
            _uiStateControl.OnStateChanged += OnStateChanged;
            _subInventoryState.OnSubInventoryUpdated += SchedulePublish;
            // ネットワーク取得完了でも再配信する
            // Republish when a network fetch completes
            _networkCache.OnUpdated += SchedulePublish;
        }

        public UniTask<string> GetSnapshotJsonAsync()
        {
            return UniTask.FromResult(BuildJson());
        }

        public void Dispose()
        {
            _disposed = true;
            _uiStateControl.OnStateChanged -= OnStateChanged;
            _subInventoryState.OnSubInventoryUpdated -= SchedulePublish;
            _networkCache.OnUpdated -= SchedulePublish;
            TrackBlock(null);
        }

        private void OnStateChanged(UIStateEnum state)
        {
            SchedulePublish();
        }

        // INFRA-7 デバウンス規約: 中間状態を畳んでフレーム末に一度だけ全量配信する
        // INFRA-7 debounce rule: fold intermediate states and publish once at end of frame
        private void SchedulePublish()
        {
            if (_publishScheduled) return;
            _publishScheduled = true;
            PublishAtEndOfFrame().Forget();
        }

        private async UniTaskVoid PublishAtEndOfFrame()
        {
            await UniTask.Yield(PlayerLoopTiming.PostLateUpdate);
            _publishScheduled = false;
            if (_disposed) return;
            _hub.Publish(TopicName, BuildJson());
        }

        private string BuildJson()
        {
            // SubInventory 状態かつ発生元がブロックのときだけ開いている扱い（列車等の非ブロックは閉）
            // Open only in the SubInventory state with a block source (non-block sources like trains are closed)
            var open = _uiStateControl.CurrentState == UIStateEnum.SubInventory;
            var sub = _subInventoryState.CurrentSubInventory;
            var blockSource = _subInventoryState.CurrentSubInventorySource as BlockSubInventorySource;

            if (!open || sub == null || blockSource == null)
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

            var dto = new BlockInventoryDto
            {
                Open = true,
                BlockType = blockSource.BlockTypeName,
                BlockName = blockSource.BlockName,
                Identifier = blockSource.BlockPosition.ToString(),
                ItemSlots = new List<BlockItemSlotDto>(sub.Count),
                FluidSlots = new List<BlockFluidSlotDto>(),
                Progress = null,
            };

            // SubInventory からスロットを写す（id/count は InventoryTopic 同型）
            // Copy slots from SubInventory; id/count mirrors InventoryTopic
            foreach (var stack in sub.SubInventory)
            {
                dto.ItemSlots.Add(new BlockItemSlotDto { ItemId = stack.Id.AsPrimitive(), Count = stack.Count });
            }

            // capability 詳細とネットワーク集約を充填する
            // Fill capability details and network aggregates
            BlockDetailDtoBuilder.Apply(dto, block, _networkCache);
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
                _networkCache.Clear();
                return;
            }

            // uGUI BlockGameObject と同じ per-block イベントタグを直接購読して再配信トリガにする
            // Directly subscribe the same per-block event tag as uGUI BlockGameObject as the republish trigger
            var eventTag = ChangeBlockStateEventPacket.CreateSpecifiedBlockEventTag(block.BlockPosInfo);
            _blockStateSubscription = ClientContext.VanillaApi.Event.SubscribeEventResponse(eventTag, _ => SchedulePublish());

            var blockType = block.BlockMasterElement.BlockType;
            // ネットワーク集約の要否は blockType で決める（spec §2-a の組み合わせ表）
            // Whether to fetch network aggregates is decided by blockType (spec §2-a combination table)
            var electric = blockType is "ElectricMachine" or "ElectricGenerator" or "ElectricMiner";
            var gear = blockType is "GearMachine" or "GearMiner" or "FuelGearGenerator" or "SimpleGearGenerator";
            var filterSplitter = blockType == "FilterSplitter";
            _networkCache.Track(block, electric, gear, filterSplitter);
        }
    }
}
