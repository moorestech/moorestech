using Client.Game.InGame.UI.Inventory.Main;
using Client.Game.InGame.UI.UIState.State;
using Client.Game.InGame.UI.UIState.State.SubInventory;
using Core.Master;
using Cysharp.Threading.Tasks;
using Game.PlayerInventory.Interface;
using Newtonsoft.Json.Linq;

namespace Client.WebUiHost.Game.Actions
{
    /// <summary>
    /// "block" エリアの slot を結合インベントリ index へ写す共通パーサ
    /// Shared parser mapping a "block"-area slot onto the combined-inventory index
    /// </summary>
    public static class BlockAreaSlotParser
    {
        public static bool TryParseBlockSlot(JToken token, SubInventoryState subInventoryState, out int combinedSlot)
        {
            combinedSlot = -1;
            if (token is not JObject obj) return false;
            if (obj["area"] is not JValue { Type: JTokenType.String } areaValue || (string)areaValue != "block") return false;

            // block の slot は必須。サブインベントリは結合インベントリの MainInventorySize 以降に並ぶ
            // block requires a slot; the sub-inventory lives after MainInventorySize in the combined inventory
            if (obj["slot"] is not JValue { Value: long slotLong }) return false;

            // 発生元がブロックのときだけ許可する。列車等の非ブロックサブは block action で操作させない
            // Allow only when the source is a block; non-block subs (e.g. trains) must not be operated via block actions
            if (subInventoryState.CurrentSubInventorySource is not BlockSubInventorySource) return false;

            // 閉状態や範囲外 slot を弾く。サブ未オープンだと結合 identifier が null で MoveItem が例外になる
            // Reject closed/out-of-range slots; with no open sub-inventory the combined identifier is null and MoveItem throws
            var sub = subInventoryState.CurrentSubInventory;
            if (sub == null) return false;
            if (slotLong < 0 || sub.Count <= slotLong) return false;

            combinedSlot = PlayerInventoryConst.MainInventorySize + (int)slotLong;
            return true;
        }
    }

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

            // 実在・数量検証は controller に集約。block も MainOrSub 結合 index で移動する
            // Presence/count validation lives in the controller; block moves also use the MainOrSub combined index
            if (!_controller.TryMoveItem(fromType, fromSlot, toType, toSlot, count, out var denyReason)) return UniTask.FromResult(ActionResult.Fail(denyReason));
            return UniTask.FromResult(ActionResult.Success());

            #region Internal

            // main/hotbar/grab は既存マッパ、block は共通パーサで結合 index へ変換する
            // main/hotbar/grab via the existing mapper; block maps onto the combined index via the shared parser
            bool TryParseAreaSlot(JToken token, out LocalMoveInventoryType type, out int localSlot)
            {
                type = LocalMoveInventoryType.MainOrSub;
                localSlot = -1;
                if (token is not JObject obj) return false;

                if (obj["area"] is not JValue { Type: JTokenType.String } areaValue) return false;
                var area = (string)areaValue;

                // block 以外は area/slot の共通パースに委譲する
                // Delegate non-block areas to the shared area/slot parser
                if (area != "block") return InventoryAreaMapper.TryParseSlotRef(token, out type, out localSlot);

                if (!BlockAreaSlotParser.TryParseBlockSlot(token, _subInventoryState, out var combinedSlot)) return false;
                type = LocalMoveInventoryType.MainOrSub;
                localSlot = combinedSlot;
                return true;
            }

            #endregion
        }
    }

    /// <summary>
    /// block_inventory.split: block スロットの半分を grab に取る（プレイヤー側 inventory.split の block 版）
    /// block_inventory.split: grab half of a block slot's stack (block-side counterpart of inventory.split)
    /// </summary>
    public class BlockSplitGrabActionHandler : IActionHandler
    {
        public string ActionType => "block_inventory.split";

        private readonly LocalPlayerInventoryController _controller;
        private readonly SubInventoryState _subInventoryState;

        public BlockSplitGrabActionHandler(LocalPlayerInventoryController controller, SubInventoryState subInventoryState)
        {
            _controller = controller;
            _subInventoryState = subInventoryState;
        }

        public UniTask<ActionResult> ExecuteAsync(JObject payload)
        {
            if (payload == null) return UniTask.FromResult(ActionResult.Fail("invalid_payload"));

            // 入力は block スロットのみ。player 側スロットは既存 inventory.split の責務
            // Input is a block slot only; player-side slots stay with the existing inventory.split
            if (!BlockAreaSlotParser.TryParseBlockSlot(payload["from"], _subInventoryState, out var combinedSlot)) return UniTask.FromResult(ActionResult.Fail("invalid_slot"));
            if (_controller.GrabInventory.Id != ItemMaster.EmptyItemId) return UniTask.FromResult(ActionResult.Fail("grab_not_empty"));

            var item = _controller.LocalPlayerInventory[combinedSlot];
            if (item.Id == ItemMaster.EmptyItemId) return UniTask.FromResult(ActionResult.Fail("empty_slot"));

            // 半分計算は SplitGrabActionHandler と同じ床関数。1個以下は成功扱いの no-op
            // Half uses the same floor as SplitGrabActionHandler; a stack of 1 is a successful no-op
            var half = item.Count / 2;
            if (0 < half) _controller.MoveItem(LocalMoveInventoryType.MainOrSub, combinedSlot, LocalMoveInventoryType.Grab, 0, half);
            return UniTask.FromResult(ActionResult.Success());
        }
    }

    /// <summary>
    /// block_inventory.collect: block スロット起点の同種収集（uGUI サブ側ダブルクリック相当）
    /// block_inventory.collect: gather same-type items from a block slot (uGUI sub-side double-click equivalent)
    /// </summary>
    public class BlockCollectActionHandler : IActionHandler
    {
        public string ActionType => "block_inventory.collect";

        private readonly LocalPlayerInventoryController _controller;
        private readonly SubInventoryState _subInventoryState;

        public BlockCollectActionHandler(LocalPlayerInventoryController controller, SubInventoryState subInventoryState)
        {
            _controller = controller;
            _subInventoryState = subInventoryState;
        }

        public UniTask<ActionResult> ExecuteAsync(JObject payload)
        {
            if (payload == null) return UniTask.FromResult(ActionResult.Fail("invalid_payload"));

            // 入力は block スロットのみ。player 側スロットは既存 inventory.collect の責務
            // Input is a block slot only; player-side slots stay with the existing inventory.collect
            if (!BlockAreaSlotParser.TryParseBlockSlot(payload["slot"], _subInventoryState, out var combinedSlot)) return UniTask.FromResult(ActionResult.Fail("invalid_slot"));

            // 収集先決定は inventory.collect と同一（host 自身の grab 状態で決める）。空手×空スロットは no-op
            // Target choice matches inventory.collect (decided by the host's own grab); empty-handed on empty is a no-op
            var grabHeld = _controller.GrabInventory.Id != ItemMaster.EmptyItemId;
            var (targetType, targetSlot) = CollectActionHandler.ResolveCollectTarget(grabHeld, combinedSlot);
            _controller.CollectItems(targetType, targetSlot);
            return UniTask.FromResult(ActionResult.Success());
        }
    }
}
