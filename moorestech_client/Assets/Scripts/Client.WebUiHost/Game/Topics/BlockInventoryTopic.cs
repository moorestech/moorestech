using System;
using System.Collections.Generic;
using Client.Game.InGame.UI.Inventory;
using Client.Game.InGame.UI.UIState;
using Client.Game.InGame.UI.UIState.State;
using Client.Game.InGame.UI.UIState.State.SubInventory;
using Client.WebUiHost.Boot;
using Client.WebUiHost.Common;
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

        public BlockInventoryTopic(WebSocketHub hub, UIStateControl uiStateControl, SubInventoryState subInventoryState)
        {
            _hub = hub;
            _uiStateControl = uiStateControl;
            _subInventoryState = subInventoryState;

            // 開閉（UIステート遷移）とスロット更新の両方を購読して push する
            // Subscribe to open/close (UI-state transitions) and slot updates, then push
            _uiStateControl.OnStateChanged += OnStateChanged;
            _subInventoryState.OnSubInventoryUpdated += OnSubInventoryUpdated;
        }

        public UniTask<string> GetSnapshotJsonAsync()
        {
            return UniTask.FromResult(BuildJson());
        }

        public void Dispose()
        {
            _uiStateControl.OnStateChanged -= OnStateChanged;
            _subInventoryState.OnSubInventoryUpdated -= OnSubInventoryUpdated;
        }

        private void OnStateChanged(UIStateEnum state)
        {
            _hub.Publish(TopicName, BuildJson());
        }

        private void OnSubInventoryUpdated()
        {
            _hub.Publish(TopicName, BuildJson());
        }

        private string BuildJson()
        {
            // SubInventory 状態でなければ閉じている扱い
            // Anything other than the SubInventory state means it is closed
            var open = _uiStateControl.CurrentState == UIStateEnum.SubInventory;
            var sub = _subInventoryState.CurrentSubInventory;
            var source = _subInventoryState.CurrentSubInventorySource;

            if (!open || sub == null)
            {
                return WebUiJson.Serialize(new BlockInventoryDto { Open = false });
            }

            var dto = new BlockInventoryDto
            {
                Open = true,
                ItemSlots = new List<BlockItemSlotDto>(sub.Count),
                FluidSlots = new List<BlockFluidSlotDto>(),
                Progress = null,
            };

            // ブロック発生元なら種別・表示名・座標識別子を埋める
            // Fill block type, display name, and position identifier when the source is a block
            if (source is BlockSubInventorySource blockSource)
            {
                dto.BlockType = blockSource.BlockTypeName;
                dto.BlockName = blockSource.BlockName;
                dto.Identifier = blockSource.BlockPosition.ToString();
            }

            // 真データ（SubInventory）からスロットを写す。InventoryTopic と同じ id/count アクセスに揃える
            // Copy slots from the true data (SubInventory), mirroring InventoryTopic's id/count access
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
