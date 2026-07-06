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

            // 実在・数量検証は controller に集約。拒否理由をそのままエラーコードに写す
            // Presence/count validation lives in the controller; map the deny reason straight to the error code
            if (!_controller.TryMoveItem(fromType, fromSlot, toType, toSlot, count, out var denyReason)) return UniTask.FromResult(ActionResult.Fail(denyReason));
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
    /// inventory.collect: クリックされたスロットを起点に同種アイテムを集める（uGUI のダブルクリック相当）
    /// inventory.collect: gather same-type items from a clicked slot (uGUI double-click equivalent)
    /// </summary>
    public class CollectActionHandler : IActionHandler
    {
        public string ActionType => "inventory.collect";

        private readonly LocalPlayerInventoryController _controller;

        public CollectActionHandler(LocalPlayerInventoryController controller)
        {
            _controller = controller;
        }

        // 収集先は host 自身の現在 grab 状態で決める。Web 側の grab 表示は dblclick 時点で必ず古いため
        // The host picks the target from its own current grab; the web's grab view is always stale at dblclick
        public static (LocalMoveInventoryType type, int slot) ResolveCollectTarget(bool grabHeld, int clickedSlot)
        {
            return grabHeld ? (LocalMoveInventoryType.Grab, 0) : (LocalMoveInventoryType.MainOrSub, clickedSlot);
        }

        public UniTask<ActionResult> ExecuteAsync(JObject payload)
        {
            if (payload == null) return UniTask.FromResult(ActionResult.Fail("invalid_payload"));

            // 入力はクリック可能スロット（main/hotbar）のみ。grab はクライアントから来ない
            // Input is a clickable slot only (main/hotbar); grab never arrives from the client
            if (!InventoryAreaMapper.TryParseClickableSlotRef(payload["slot"], out var clickedSlot)) return UniTask.FromResult(ActionResult.Fail("invalid_slot"));

            // 収集先決定は uGUI の DoubleClick と同一。空手×空スロットは CollectItems が no-op（成功扱い）
            // Target choice mirrors uGUI DoubleClick; empty-handed on an empty slot is a CollectItems no-op (success)
            var grabHeld = _controller.GrabInventory.Id != ItemMaster.EmptyItemId;
            var (targetType, targetSlot) = ResolveCollectTarget(grabHeld, clickedSlot);
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
