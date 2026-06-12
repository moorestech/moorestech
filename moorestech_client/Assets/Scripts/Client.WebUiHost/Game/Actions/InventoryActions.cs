using Client.Game.InGame.UI.Inventory.Main;
using Core.Master;
using Cysharp.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace Client.WebUiHost.Game.Actions
{
    /// <summary>
    /// inventory.move_item: from→to へ count 個移動する
    /// inventory.move_item: move count items from→to
    /// </summary>
    public class MoveItemActionHandler : IActionHandler
    {
        public string ActionType => "inventory.move_item";

        private readonly LocalPlayerInventoryController _controller;

        public MoveItemActionHandler(LocalPlayerInventoryController controller)
        {
            _controller = controller;
        }

        public UniTask<ActionResult> ExecuteAsync(JObject payload)
        {
            if (payload == null) return UniTask.FromResult(ActionResult.Fail("invalid_payload"));

            var countToken = payload["count"];
            if (countToken is not JValue { Value: long countLong } || countLong <= 0 || int.MaxValue < countLong) return UniTask.FromResult(ActionResult.Fail("invalid_count"));
            var count = (int)countLong;

            if (!InventoryAreaMapper.TryParseSlotRef(payload["from"], out var fromType, out var fromSlot)) return UniTask.FromResult(ActionResult.Fail("invalid_slot"));
            if (!InventoryAreaMapper.TryParseSlotRef(payload["to"], out var toType, out var toSlot)) return UniTask.FromResult(ActionResult.Fail("invalid_slot"));

            // 同一スロットへの移動はMoveItem内部でアイテムが消失するため no-op にする
            // Same-slot moves corrupt the stack inside MoveItem, so treat them as a no-op
            if (fromType == toType && fromSlot == toSlot) return UniTask.FromResult(ActionResult.Success());

            // 移動元の実在チェック。空・数量不足は安定したエラーコードで返す
            // Validate the source stack; report empty / insufficient stacks with stable error codes
            var fromItem = fromType == LocalMoveInventoryType.Grab ? _controller.GrabInventory : _controller.LocalPlayerInventory[fromSlot];
            if (fromItem.Id == ItemMaster.EmptyItemId) return UniTask.FromResult(ActionResult.Fail("empty_slot"));
            if (fromItem.Count < count) return UniTask.FromResult(ActionResult.Fail("insufficient_count"));

            _controller.MoveItem(fromType, fromSlot, toType, toSlot, count);
            return UniTask.FromResult(ActionResult.Success());
        }
    }

    /// <summary>
    /// inventory.split: スロットの半分を grab に取る（uGUI の右クリック相当）
    /// inventory.split: grab half of a slot's stack (uGUI right-click equivalent)
    /// </summary>
    public class SplitGrabActionHandler : IActionHandler
    {
        public string ActionType => "inventory.split";

        private readonly LocalPlayerInventoryController _controller;

        public SplitGrabActionHandler(LocalPlayerInventoryController controller)
        {
            _controller = controller;
        }

        public UniTask<ActionResult> ExecuteAsync(JObject payload)
        {
            if (payload == null) return UniTask.FromResult(ActionResult.Fail("invalid_payload"));

            if (!InventoryAreaMapper.TryParseSlotRef(payload["from"], out var fromType, out var fromSlot)) return UniTask.FromResult(ActionResult.Fail("invalid_slot"));
            if (fromType != LocalMoveInventoryType.MainOrSub) return UniTask.FromResult(ActionResult.Fail("invalid_slot"));
            if (_controller.GrabInventory.Id != ItemMaster.EmptyItemId) return UniTask.FromResult(ActionResult.Fail("grab_not_empty"));

            var item = _controller.LocalPlayerInventory[fromSlot];
            if (item.Id == ItemMaster.EmptyItemId) return UniTask.FromResult(ActionResult.Fail("empty_slot"));

            // 1個以下なら半分は0なので何もしない（成功扱い）
            // A stack of 1 has no half; treat as a successful no-op
            var half = item.Count / 2;
            if (0 < half) _controller.MoveItem(LocalMoveInventoryType.MainOrSub, fromSlot, LocalMoveInventoryType.Grab, 0, half);
            return UniTask.FromResult(ActionResult.Success());
        }
    }

    /// <summary>
    /// inventory.collect: 同種アイテムを target に集める（uGUI のダブルクリック相当）
    /// inventory.collect: gather same-type items into target (uGUI double-click equivalent)
    /// </summary>
    public class CollectActionHandler : IActionHandler
    {
        public string ActionType => "inventory.collect";

        private readonly LocalPlayerInventoryController _controller;

        public CollectActionHandler(LocalPlayerInventoryController controller)
        {
            _controller = controller;
        }

        public UniTask<ActionResult> ExecuteAsync(JObject payload)
        {
            if (payload == null) return UniTask.FromResult(ActionResult.Fail("invalid_payload"));

            if (!InventoryAreaMapper.TryParseSlotRef(payload["target"], out var targetType, out var targetSlot)) return UniTask.FromResult(ActionResult.Fail("invalid_slot"));

            // 空ターゲットはエラーで返し、収集本体はコントローラに委譲する
            // Reject empty targets with an error; the collection itself is delegated to the controller
            var isGrabTarget = targetType == LocalMoveInventoryType.Grab;
            var collectTarget = isGrabTarget ? _controller.GrabInventory : _controller.LocalPlayerInventory[targetSlot];
            if (collectTarget.Id == ItemMaster.EmptyItemId) return UniTask.FromResult(ActionResult.Fail("empty_slot"));

            _controller.CollectItems(targetType, targetSlot);
            return UniTask.FromResult(ActionResult.Success());
        }
    }

    /// <summary>
    /// inventory.sort: インベントリ整理（uGUI の整理ボタン相当）
    /// inventory.sort: tidy the inventory (uGUI sort-button equivalent)
    /// </summary>
    public class SortInventoryActionHandler : IActionHandler
    {
        public string ActionType => "inventory.sort";

        private readonly LocalPlayerInventoryController _controller;

        public SortInventoryActionHandler(LocalPlayerInventoryController controller)
        {
            _controller = controller;
        }

        public UniTask<ActionResult> ExecuteAsync(JObject payload)
        {
            _controller.SortInventory();
            return UniTask.FromResult(ActionResult.Success());
        }
    }
}
