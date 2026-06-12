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
        private bool _disposed;

        public InventoryTopic(WebSocketHub hub, LocalPlayerInventoryController controller)
        {
            _hub = hub;
            _controller = controller;

            // インデクサ経由の変更と、grab/全置換の更新の両方を購読する
            // Subscribe to both indexer-driven changes and grab/full-replacement refreshes
            _subscription = new CompositeDisposable(
                _controller.LocalPlayerInventory.OnItemChange.Subscribe(_ => SchedulePublish()),
                _controller.OnInventoryRefreshed.Subscribe(_ => SchedulePublish()));
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
        }

        private async UniTaskVoid PublishAtEndOfFrame()
        {
            await UniTask.Yield(PlayerLoopTiming.PostLateUpdate);
            _publishScheduled = false;

            // Dispose 後に遅延 publish が走らないようガードする
            // Guard so a deferred publish never fires after Dispose
            if (_disposed) return;
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
