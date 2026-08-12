using System;
using Client.Game.InGame.Hotbar;
using Cysharp.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace Client.WebUiHost.Game.Actions
{
    /// <summary>
    /// hotbar.select: クリック選択をUIStateの消費待ちキューへ積む
    /// hotbar.select: queue a click selection for UIState to consume
    /// </summary>
    public class HotbarSelectActionHandler : IActionHandler
    {
        public string ActionType => "hotbar.select";

        private readonly ClientHotbarDatastore _clientHotbarDatastore;

        public HotbarSelectActionHandler(ClientHotbarDatastore clientHotbarDatastore)
        {
            _clientHotbarDatastore = clientHotbarDatastore;
        }

        public UniTask<ActionResult> ExecuteAsync(JObject payload)
        {
            if (!HotbarActionPayload.TryParseSlot(payload, "index", _clientHotbarDatastore.Assignments.Count, out var index))
                return UniTask.FromResult(ActionResult.Fail("invalid_index"));

            _clientHotbarDatastore.EnqueueSelectRequest(index);
            return UniTask.FromResult(ActionResult.Success());
        }
    }

    /// <summary>
    /// hotbar.assign: 枠へ設置対象IDを割り当てる
    /// hotbar.assign: assign a placement-target id to a slot
    /// </summary>
    public class HotbarAssignActionHandler : IActionHandler
    {
        public string ActionType => "hotbar.assign";

        private readonly ClientHotbarDatastore _clientHotbarDatastore;

        public HotbarAssignActionHandler(ClientHotbarDatastore clientHotbarDatastore)
        {
            _clientHotbarDatastore = clientHotbarDatastore;
        }

        public UniTask<ActionResult> ExecuteAsync(JObject payload)
        {
            if (!HotbarActionPayload.TryParseSlot(payload, "slot", _clientHotbarDatastore.Assignments.Count, out var slot))
                return UniTask.FromResult(ActionResult.Fail("invalid_slot"));

            // idのGuidパース失敗は無視する（要件裁定: D&D元は常に正規カタログidのため、不整合をエラー化しない）
            // A Guid parse failure on id is ignored (arbitrated: the drag source is always a catalog id, so never error the mismatch)
            if (payload["id"] is not JValue { Type: JTokenType.String } idValue || !Guid.TryParse((string)idValue, out var targetId))
                return UniTask.FromResult(ActionResult.Success());

            _clientHotbarDatastore.RequestAssign(slot, targetId);
            return UniTask.FromResult(ActionResult.Success());
        }
    }

    /// <summary>
    /// hotbar.clear: 枠の割当を外す
    /// hotbar.clear: clear a slot's assignment
    /// </summary>
    public class HotbarClearActionHandler : IActionHandler
    {
        public string ActionType => "hotbar.clear";

        private readonly ClientHotbarDatastore _clientHotbarDatastore;

        public HotbarClearActionHandler(ClientHotbarDatastore clientHotbarDatastore)
        {
            _clientHotbarDatastore = clientHotbarDatastore;
        }

        public UniTask<ActionResult> ExecuteAsync(JObject payload)
        {
            if (!HotbarActionPayload.TryParseSlot(payload, "slot", _clientHotbarDatastore.Assignments.Count, out var slot))
                return UniTask.FromResult(ActionResult.Fail("invalid_slot"));

            _clientHotbarDatastore.RequestClear(slot);
            return UniTask.FromResult(ActionResult.Success());
        }
    }

    /// <summary>
    /// hotbar.swap: 2枠の割当を入れ替える
    /// hotbar.swap: swap the assignments of two slots
    /// </summary>
    public class HotbarSwapActionHandler : IActionHandler
    {
        public string ActionType => "hotbar.swap";

        private readonly ClientHotbarDatastore _clientHotbarDatastore;

        public HotbarSwapActionHandler(ClientHotbarDatastore clientHotbarDatastore)
        {
            _clientHotbarDatastore = clientHotbarDatastore;
        }

        public UniTask<ActionResult> ExecuteAsync(JObject payload)
        {
            var slotCount = _clientHotbarDatastore.Assignments.Count;
            if (!HotbarActionPayload.TryParseSlot(payload, "from", slotCount, out var from) ||
                !HotbarActionPayload.TryParseSlot(payload, "to", slotCount, out var to))
                return UniTask.FromResult(ActionResult.Fail("invalid_slot"));

            _clientHotbarDatastore.RequestSwap(from, to);
            return UniTask.FromResult(ActionResult.Success());
        }
    }

    /// <summary>
    /// hotbar.* 4ハンドラ共通のスロット番号パース
    /// Shared slot-number parsing for the 4 hotbar.* handlers
    /// </summary>
    internal static class HotbarActionPayload
    {
        public static bool TryParseSlot(JObject payload, string key, int slotCount, out int slot)
        {
            slot = default;
            if (payload?[key] is not JValue { Value: long slotLong } || slotLong < 0 || slotCount <= slotLong) return false;
            slot = (int)slotLong;
            return true;
        }
    }
}
