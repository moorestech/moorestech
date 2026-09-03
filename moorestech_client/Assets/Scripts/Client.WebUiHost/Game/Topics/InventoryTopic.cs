using System;
using System.Collections.Generic;
using Client.Game.InGame.UI.Inventory.Equipment;
using Client.Game.InGame.UI.Inventory.Main;
using Client.WebUiHost.Boot;
using Client.WebUiHost.Common;
using Core.Item.Interface;
using Cysharp.Threading.Tasks;
using UniRx;

namespace Client.WebUiHost.Game.Topics
{
    /// <summary>
    /// local_player.inventory: 全量push
    /// local_player.inventory topic: pushes the full main/grab/equipment state
    /// </summary>
    public class InventoryTopic : ITopicHandler, IDisposable
    {
        public const string TopicName = "local_player.inventory";

        private readonly WebSocketHub _hub;
        private readonly LocalPlayerInventoryController _controller;
        private readonly LocalPlayerEquipment _equipment;
        private readonly IDisposable _subscription;
        private bool _publishScheduled;
        private bool _disposed;

        public InventoryTopic(WebSocketHub hub, LocalPlayerInventoryController controller, LocalPlayerEquipment equipment)
        {
            _hub = hub;
            _controller = controller;
            _equipment = equipment;

            // インデクサ経由の変更・grab/全置換の更新・装備のスロット/選択変更を購読する
            // Subscribe to indexer-driven changes, grab/full-replacement refreshes, and equipment slot/selection changes
            _subscription = new CompositeDisposable(
                _controller.LocalPlayerInventory.OnItemChange.Subscribe(_ => SchedulePublish()),
                _controller.OnInventoryRefreshed.Subscribe(_ => SchedulePublish()),
                _equipment.OnSlotsOrSelectionChanged.Subscribe(_ => SchedulePublish()));
        }

        public UniTask<string> GetSnapshotJsonAsync()
        {
            return UniTask.FromResult(BuildJson());
        }

        public void Dispose()
        {
            _disposed = true;
            _subscription.Dispose();
        }

        // MoveItem 途中の中間状態（grab 未更新等）を配信しないようフレーム末尾でまとめて publish する
        // Defer publishing to end of frame so mid-MoveItem intermediate states never go out
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

                // Dispose 後に遅延 publish が走らないようガードする
                // Guard so a deferred publish never fires after Dispose
                if (_disposed) return;
                _hub.Publish(TopicName, BuildJson());
            }

            #endregion
        }

        private string BuildJson()
        {
            var inv = _controller.LocalPlayerInventory;
            var mainSlotCount = inv.MainSlotCount;
            var dto = new PlayerInventoryDto
            {
                MainSlots = new List<SlotDto>(mainSlotCount),
                Grab = ToDto(_controller.GrabInventory),
                // 装備枠数はマスタ由来。選択は常に実スロットのインデックス
                // The equipment slot count comes from the master; the selection is always a real slot index
                Equipment = new List<SlotDto>(_equipment.Slots.Count),
                SelectedEquipment = _equipment.SelectedIndex,
                EquipmentSelectionConfirmationRevision = _equipment.SelectionConfirmationRevision,
            };
            for (var i = 0; i < mainSlotCount; i++) dto.MainSlots.Add(ToDto(inv[i]));
            foreach (var equipmentSlot in _equipment.Slots) dto.Equipment.Add(ToDto(equipmentSlot));
            return WebUiJson.Serialize(dto);

            #region Internal

            static SlotDto ToDto(IItemStack stack)
            {
                return new SlotDto { ItemId = stack.Id.AsPrimitive(), Count = stack.Count };
            }

            #endregion
        }
    }

    /// <summary>
    /// local_player.inventory の配信 DTO
    /// Payload DTO for local_player.inventory
    /// </summary>
    public class PlayerInventoryDto
    {
        public List<SlotDto> MainSlots;
        public SlotDto Grab;
        public List<SlotDto> Equipment;
        public int SelectedEquipment;
        public int EquipmentSelectionConfirmationRevision;
    }

    public class SlotDto
    {
        public int ItemId;
        public int Count;
    }
}
