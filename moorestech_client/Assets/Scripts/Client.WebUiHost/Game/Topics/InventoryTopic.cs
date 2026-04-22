using System;
using System.Text;
using Client.Game.InGame.UI.Inventory.Main;
using Client.WebUiHost.Boot;
using Core.Item.Interface;
using Cysharp.Threading.Tasks;
using UniRx;

namespace Client.WebUiHost.Game.Topics
{
    /// <summary>
    /// local_player.inventory トピック: スロット変更のたびに全量を push
    /// local_player.inventory topic: pushes the full inventory on every slot change
    /// </summary>
    public class InventoryTopic : ITopicHandler, IDisposable
    {
        public const string TopicName = "local_player.inventory";

        private readonly WebSocketHub _hub;
        private readonly LocalPlayerInventoryController _controller;
        private readonly IDisposable _subscription;

        public InventoryTopic(WebSocketHub hub, LocalPlayerInventoryController controller)
        {
            _hub = hub;
            _controller = controller;

            // スロット変更通知を購読し、Dispose 時に解除できるよう保持
            // Subscribe to slot-change notifications; retain the disposable so Dispose can unhook
            _subscription = _controller.LocalPlayerInventory.OnItemChange.Subscribe(_ => Publish());
        }

        public UniTask<string> GetSnapshotJsonAsync()
        {
            return UniTask.FromResult(BuildJson(_controller.LocalPlayerInventory));
        }

        public void Dispose()
        {
            _subscription.Dispose();
        }

        private void Publish()
        {
            var json = BuildJson(_controller.LocalPlayerInventory);
            _hub.Publish(TopicName, json);
        }

        private static string BuildJson(ILocalPlayerInventory inv)
        {
            // メイン/ホットバーの分割は未実装のため、全スロットを単一配列として出す
            // Main/hotbar split is not yet implemented, so emit all slots as a single array
            var sb = new StringBuilder();
            sb.Append("{\"slots\":[");
            for (var i = 0; i < inv.Count; i++)
            {
                var stack = inv[i];
                if (i > 0) sb.Append(',');
                sb.Append("{\"itemId\":");
                sb.Append(stack.Id.AsPrimitive());
                sb.Append(",\"count\":");
                sb.Append(stack.Count);
                sb.Append('}');
            }
            sb.Append("]}");
            return sb.ToString();
        }
    }
}
