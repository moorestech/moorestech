using System;
using System.Collections.Generic;
using Client.Game.InGame.UI.Inventory;
using Client.Game.InGame.UI.UIState;
using Client.Game.InGame.UI.UIState.State;
using Client.Game.InGame.UI.UIState.State.SubInventory;
using Client.WebUiHost.Boot;
using Client.WebUiHost.Common;
using Client.WebUiHost.Game.Topics.BlockDetail;
using Cysharp.Threading.Tasks;

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
            _subInventoryState.OnSubInventoryUpdated += SchedulePublish;
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
                return WebUiJson.Serialize(new BlockInventoryDto { Open = false });
            }

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

            return WebUiJson.Serialize(dto);
        }
    }

    /// <summary>
    /// block_inventory.current の配信 DTO
    /// Payload DTO for block_inventory.current
    /// </summary>
    public class BlockInventoryDto
    {
        public bool Open;
        public string BlockType;
        public string Identifier;
        public string BlockName;
        public List<BlockItemSlotDto> ItemSlots;
        public List<BlockFluidSlotDto> FluidSlots;
        public double? Progress;
        // capability 詳細（該当ブロックのみ。null はキー省略される）
        // Capability details (only for applicable blocks; null keys are omitted)
        public MachineDetailDto Machine;
        public GeneratorDetailDto Generator;
        public MinerDetailDto Miner;
        public GearDetailDto Gear;
        public ElectricNetworkDto ElectricNetwork;
        public GearNetworkDto GearNetwork;
        public FilterSplitterDto FilterSplitter;
    }

    public class BlockItemSlotDto
    {
        public int ItemId;
        public int Count;
    }

    public class BlockFluidSlotDto
    {
        public int FluidId;
        public double Amount;
        public double Capacity;
        public string Name;
    }
}
