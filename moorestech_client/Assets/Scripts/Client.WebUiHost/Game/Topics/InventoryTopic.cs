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
    public class InventoryTopic : ITopicHandler
    {
        public const string TopicName = "local_player.inventory";

        private readonly WebSocketHub _hub;
        private readonly LocalPlayerInventoryController _controller;

        public InventoryTopic(WebSocketHub hub, LocalPlayerInventoryController controller)
        {
            _hub = hub;
            _controller = controller;

            // スロット変更通知を購読して全量を配信
            // Subscribe to slot-change notifications and broadcast the full inventory
            _controller.LocalPlayerInventory.OnItemChange.Subscribe(_ => Publish());
        }

        public UniTask<string> GetSnapshotJsonAsync()
        {
            return UniTask.FromResult(BuildJson(_controller.LocalPlayerInventory));
        }

        private void Publish()
        {
            var json = BuildJson(_controller.LocalPlayerInventory);
            _hub.Publish(TopicName, json);
        }

        private static string BuildJson(ILocalPlayerInventory inv)
        {
            // ホットバーは既存定数がある場合はそちらに従うが、本スペックでは
            // 単純化のため全スロットを mainSlots として出力する。
            // Hotbar splitting will be refined later; for this spec we dump
            // every slot into mainSlots to keep things simple.
            var sb = new StringBuilder();
            sb.Append("{\"mainSlots\":[");
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
            sb.Append("],\"hotBarSlots\":[]}");
            return sb.ToString();
        }
    }
}
