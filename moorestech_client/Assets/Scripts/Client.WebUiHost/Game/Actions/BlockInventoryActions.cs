using Client.Game.InGame.UI.Inventory.Main;
using Client.Game.InGame.UI.UIState.State;
using Core.Master;
using Cysharp.Threading.Tasks;
using Game.PlayerInventory.Interface;
using Newtonsoft.Json.Linq;

namespace Client.WebUiHost.Game.Actions
{
    /// <summary>
    /// block_inventory.move_item: from→to へ count 個移動する（main/hotbar/grab/block 対応）
    /// block_inventory.move_item: move count items from→to (supports main/hotbar/grab/block)
    /// </summary>
    public class BlockMoveItemActionHandler : IActionHandler
    {
        public string ActionType => "block_inventory.move_item";

        private readonly LocalPlayerInventoryController _controller;
        private readonly SubInventoryState _subInventoryState;

        public BlockMoveItemActionHandler(LocalPlayerInventoryController controller, SubInventoryState subInventoryState)
        {
            _controller = controller;
            _subInventoryState = subInventoryState;
        }

        public UniTask<ActionResult> ExecuteAsync(JObject payload)
        {
            if (payload == null) return UniTask.FromResult(ActionResult.Fail("invalid_payload"));

            var countToken = payload["count"];
            if (countToken is not JValue { Value: long countLong } || countLong <= 0 || int.MaxValue < countLong) return UniTask.FromResult(ActionResult.Fail("invalid_count"));
            var count = (int)countLong;

            if (!TryParseAreaSlot(payload["from"], out var fromType, out var fromSlot)) return UniTask.FromResult(ActionResult.Fail("invalid_slot"));
            if (!TryParseAreaSlot(payload["to"], out var toType, out var toSlot)) return UniTask.FromResult(ActionResult.Fail("invalid_slot"));

            // 同一スロットへの移動は MoveItem 内部でアイテムが消失するため no-op にする
            // Same-slot moves corrupt the stack inside MoveItem, so treat them as a no-op
            if (fromType == toType && fromSlot == toSlot) return UniTask.FromResult(ActionResult.Success());

            // 移動元の実在チェック。空・数量不足は安定したエラーコードで返す
            // Validate the source stack; report empty / insufficient stacks with stable error codes
            var fromItem = fromType == LocalMoveInventoryType.Grab ? _controller.GrabInventory : _controller.LocalPlayerInventory[fromSlot];
            if (fromItem.Id == ItemMaster.EmptyItemId) return UniTask.FromResult(ActionResult.Fail("empty_slot"));
            if (fromItem.Count < count) return UniTask.FromResult(ActionResult.Fail("insufficient_count"));

            // ブロック(=サブ)スロットへの移動も MainOrSub の結合インデックスで MoveItem が処理する
            // Moves to a block (sub) slot are handled by MoveItem via the MainOrSub combined index
            _controller.MoveItem(fromType, fromSlot, toType, toSlot, count);
            return UniTask.FromResult(ActionResult.Success());
        }

        // main/hotbar/grab は既存マッパ、block はサブインベントリ結合インデックスへ変換する
        // main/hotbar/grab via the existing mapper; block maps to the sub-inventory combined index
        private bool TryParseAreaSlot(JToken token, out LocalMoveInventoryType type, out int localSlot)
        {
            type = LocalMoveInventoryType.MainOrSub;
            localSlot = -1;
            if (token is not JObject obj) return false;

            if (obj["area"] is not JValue { Type: JTokenType.String } areaValue) return false;
            var area = (string)areaValue;

            // block 以外は area/slot の共通パースに委譲する
            // Delegate non-block areas to the shared area/slot parser
            if (area != "block") return InventoryAreaMapper.TryParseSlotRef(token, out type, out localSlot);

            // block の slot は必須。サブインベントリは結合インベントリの MainInventorySize 以降に並ぶ
            // block requires a slot; the sub-inventory lives after MainInventorySize in the combined inventory
            if (obj["slot"] is not JValue { Value: long slotLong }) return false;

            // 閉状態や範囲外 slot を弾く。サブ未オープンだと結合 identifier が null で MoveItem が例外になる
            // Reject closed/out-of-range slots; with no open sub-inventory the combined identifier is null and MoveItem throws
            var sub = _subInventoryState.CurrentSubInventory;
            if (sub == null) return false;
            if (slotLong < 0 || sub.Count <= slotLong) return false;

            type = LocalMoveInventoryType.MainOrSub;
            localSlot = PlayerInventoryConst.MainInventorySize + (int)slotLong;
            return true;
        }
    }
}
