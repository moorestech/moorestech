using System;
using System.Collections.Generic;
using Client.Game.InGame.Context;
using Client.Game.InGame.UI.Inventory;
using Client.Game.InGame.UI.UIState;
using Client.Game.InGame.UI.UIState.State;
using Client.Game.InGame.UI.UIState.State.SubInventory;
using Client.WebUiHost.Boot;
using Client.WebUiHost.Common;
using Core.Master;
using Cysharp.Threading.Tasks;
using Game.Block.Interface.State;
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

        private readonly WebSocketHub _hub;
        private readonly UIStateControl _uiStateControl;
        private readonly SubInventoryState _subInventoryState;
        private readonly IDisposable _subInventorySubscription;

        // 開いているブロックの状態変化（流体量・進捗）を購読する。ブロックが変わるたび張り替える
        // Subscribes to the open block's state changes (fluid amount, progress); rebound whenever the block changes
        private IDisposable _blockStateSubscription;
        private string _subscribedEventTag;
        private bool _publishScheduled;
        private bool _disposed;

        public BlockInventoryTopic(WebSocketHub hub, UIStateControl uiStateControl, SubInventoryState subInventoryState)
        {
            _hub = hub;
            _uiStateControl = uiStateControl;
            _subInventoryState = subInventoryState;

            // 開閉（UIステート遷移）とスロット更新の両方を購読して push する
            // Subscribe to open/close (UI-state transitions) and slot updates, then push
            _uiStateControl.OnStateChanged += OnStateChanged;
            _subInventorySubscription = _subInventoryState.OnSubInventoryUpdated.Subscribe(_ => OnSubInventoryUpdated());
        }

        public UniTask<string> GetSnapshotJsonAsync()
        {
            SyncBlockStateSubscription();
            return UniTask.FromResult(BuildJson());
        }

        public void Dispose()
        {
            _disposed = true;
            _uiStateControl.OnStateChanged -= OnStateChanged;
            _subInventorySubscription.Dispose();
            _blockStateSubscription?.Dispose();
        }

        private void OnStateChanged(UIStateEnum state)
        {
            SyncBlockStateSubscription();
            SchedulePublish();
        }

        private void OnSubInventoryUpdated()
        {
            SyncBlockStateSubscription();
            SchedulePublish();
        }

        // 現在開いているブロックの状態イベントへ購読を合わせる。対象が変わったときだけ張り替える
        // Align the subscription to the currently open block's state event; rebind only when the target changes
        private void SyncBlockStateSubscription()
        {
            var open = _uiStateControl.CurrentState == UIStateEnum.SubInventory;
            var blockSource = _subInventoryState.CurrentSubInventorySource as BlockSubInventorySource;
            var targetTag = open && blockSource != null ? blockSource.CreateBlockStateEventTag() : null;
            if (targetTag == _subscribedEventTag) return;

            _blockStateSubscription?.Dispose();
            _blockStateSubscription = null;
            _subscribedEventTag = null;
            if (targetTag == null) return;

            _blockStateSubscription = ClientContext.VanillaApi.Event.SubscribeEventResponse(targetTag, _ => SchedulePublish());
            _subscribedEventTag = targetTag;
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

            if (!open || sub == null || blockSource == null)
            {
                return WebUiJson.Serialize(new BlockInventoryDto { Open = false });
            }

            var dto = new BlockInventoryDto
            {
                Open = true,
                BlockType = blockSource.BlockTypeName,
                BlockName = blockSource.BlockName,
                Identifier = blockSource.BlockPosition.ToString(),
                ItemSlots = new List<BlockItemSlotDto>(sub.Count),
                FluidSlots = BuildFluidSlots(blockSource),
                Progress = BuildProgress(blockSource),
            };

            // SubInventory からスロットを写す（id/count は InventoryTopic 同型）
            // Copy slots from SubInventory; id/count mirrors InventoryTopic
            foreach (var stack in sub.SubInventory)
            {
                dto.ItemSlots.Add(new BlockItemSlotDto { ItemId = stack.Id.AsPrimitive(), Count = stack.Count });
            }

            return WebUiJson.Serialize(dto);

            #region Internal

            List<BlockFluidSlotDto> BuildFluidSlots(BlockSubInventorySource source)
            {
                // 流体状態が無いブロック（チェスト等）は空リスト
                // Blocks without fluid state (chests etc.) yield an empty list
                var slots = new List<BlockFluidSlotDto>();
                var fluidState = source.GetFluidInventoryStateOrNull();
                if (fluidState == null) return slots;

                // 入力→出力の順で全タンクを写す。空タンクも容量表示のため残す
                // Copy all tanks in input-then-output order; keep empty tanks to show capacity
                AddTanks(fluidState.InputTanks);
                AddTanks(fluidState.OutputTanks);
                return slots;

                void AddTanks(List<FluidMessagePack> tanks)
                {
                    if (tanks == null) return;
                    foreach (var tank in tanks)
                    {
                        slots.Add(new BlockFluidSlotDto
                        {
                            FluidId = tank.FluidId,
                            Amount = tank.Amount,
                            Capacity = tank.MaxCapacity,
                            Name = ResolveFluidName(tank.FluidId),
                        });
                    }
                }
            }

            string ResolveFluidName(int fluidId)
            {
                // 空タンクは名前なし。それ以外はマスタから表示名を引く
                // Empty tanks have no name; otherwise resolve the display name from master
                var id = new FluidId(fluidId);
                if (id == FluidMaster.EmptyFluidId) return null;
                return MasterHolder.FluidMaster.GetFluidMaster(id).Name;
            }

            double? BuildProgress(BlockSubInventorySource source)
            {
                // 機械のみ加工進捗を持つ。非機械は null（キー省略）
                // Only machines have processing progress; non-machines yield null (key omitted)
                var machineState = source.GetMachineStateOrNull();
                if (machineState == null) return null;
                return machineState.ProcessingRate;
            }

            #endregion
        }
    }
}
