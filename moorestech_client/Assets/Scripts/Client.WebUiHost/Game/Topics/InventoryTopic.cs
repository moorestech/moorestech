using System;
using System.Collections.Generic;
using Client.Game.InGame.UI.Inventory.Main;
using Client.WebUiHost.Boot;
using Client.WebUiHost.Common;
using Client.WebUiHost.Game.Actions;
using Core.Item.Interface;
using Cysharp.Threading.Tasks;
using Game.PlayerInventory.Interface;
using UniRx;

namespace Client.WebUiHost.Game.Topics
{
    /// <summary>
    /// local_player.inventory トピック: main/hotbar/grab の全量を push
    /// local_player.inventory topic: pushes the full main/hotbar/grab state
    /// </summary>
    public class InventoryTopic : ITopicHandler, IDisposable
    {
        public const string TopicName = "local_player.inventory";

        private readonly WebSocketHub _hub;
        private readonly LocalPlayerInventoryController _controller;
        private readonly IDisposable _subscription;
        private bool _publishScheduled;

        public InventoryTopic(WebSocketHub hub, LocalPlayerInventoryController controller)
        {
            _hub = hub;
            _controller = controller;

            // スロット変更通知を購読し、Dispose 時に解除できるよう保持
            // Subscribe to slot-change notifications; retain the disposable so Dispose can unhook
            _subscription = _controller.LocalPlayerInventory.OnItemChange.Subscribe(_ => SchedulePublish());
        }

        public UniTask<string> GetSnapshotJsonAsync()
        {
            return UniTask.FromResult(BuildJson());
        }

        public void Dispose()
        {
            _subscription.Dispose();
        }

        // MoveItem 途中の中間状態（grab 未更新等）を配信しないようフレーム末尾でまとめて publish する
        // Defer publishing to end of frame so mid-MoveItem intermediate states never go out
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
            _hub.Publish(TopicName, BuildJson());
        }

        private string BuildJson()
        {
            var inv = _controller.LocalPlayerInventory;
            var dto = new PlayerInventoryDto
            {
                MainSlots = new List<SlotDto>(InventoryAreaMapper.MainAreaSize),
                HotbarSlots = new List<SlotDto>(PlayerInventoryConst.MainInventoryColumns),
                Grab = ToDto(_controller.GrabInventory),
            };
            for (var i = 0; i < InventoryAreaMapper.MainAreaSize; i++) dto.MainSlots.Add(ToDto(inv[i]));
            for (var i = InventoryAreaMapper.MainAreaSize; i < PlayerInventoryConst.MainInventorySize; i++) dto.HotbarSlots.Add(ToDto(inv[i]));
            return WebUiJson.Serialize(dto);
        }

        private static SlotDto ToDto(IItemStack stack)
        {
            return new SlotDto { ItemId = stack.Id.AsPrimitive(), Count = stack.Count };
        }
    }

    /// <summary>
    /// local_player.inventory の配信 DTO
    /// Payload DTO for local_player.inventory
    /// </summary>
    public class PlayerInventoryDto
    {
        public List<SlotDto> MainSlots;
        public List<SlotDto> HotbarSlots;
        public SlotDto Grab;
    }

    public class SlotDto
    {
        public int ItemId;
        public int Count;
    }
}
