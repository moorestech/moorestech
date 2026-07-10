using System.Linq;
using System.Threading;
using Client.Game.InGame.BlockSystem.PlaceSystem.Blueprint;
using Client.Game.InGame.UI.BuildMenu;
using Client.Game.InGame.UI.UIState;
using Client.WebUiHost.Game.Topics.BuildMenu;
using Cysharp.Threading.Tasks;
using Game.UnlockState;
using Newtonsoft.Json.Linq;

namespace Client.WebUiHost.Game.Actions
{
    /// <summary>
    /// build_menu.select: web の選択を uGUI の選択消費キューへ投入する
    /// build_menu.select: feeds a web selection into the uGUI consume queue
    /// </summary>
    public class BuildMenuSelectActionHandler : IActionHandler
    {
        public string ActionType => "build_menu.select";

        private readonly UIStateControl _uiStateControl;
        private readonly IGameUnlockStateData _unlockState;
        private readonly ClientBlueprintLibrary _blueprintLibrary;
        private readonly BuildMenuView _buildMenuView;

        public BuildMenuSelectActionHandler(UIStateControl uiStateControl, IGameUnlockStateData unlockState, ClientBlueprintLibrary blueprintLibrary, BuildMenuView buildMenuView)
        {
            _uiStateControl = uiStateControl;
            _unlockState = unlockState;
            _blueprintLibrary = blueprintLibrary;
            _buildMenuView = buildMenuView;
        }

        public UniTask<ActionResult> ExecuteAsync(JObject payload)
        {
            if (payload == null) return UniTask.FromResult(ActionResult.Fail("invalid_payload"));
            if (payload["entryType"] is not JValue { Type: JTokenType.String } typeValue) return UniTask.FromResult(ActionResult.Fail("invalid_payload"));
            if (payload["entryKey"] is not JValue { Type: JTokenType.String } keyValue) return UniTask.FromResult(ActionResult.Fail("invalid_payload"));

            // BuildMenu以外でのstale到達（Unity側が先に閉じたレース）は拒否する
            // Reject stale arrivals outside BuildMenu (the Unity side closed the menu first)
            if (_uiStateControl.CurrentState != UIStateEnum.BuildMenu) return UniTask.FromResult(ActionResult.Fail("invalid_state"));

            // 現在のカタログと種別+キーで照合し、削除済みBP等へのstaleクリックを弾く
            // Match against the current catalog by type+key, rejecting stale clicks (e.g. deleted blueprints)
            var entryType = (string)typeValue;
            var entryKey = (string)keyValue;
            var entries = BuildMenuEntryCatalog.CreateEntries(_unlockState, _blueprintLibrary);
            var matched = entries.Where(e => BuildMenuEntryDtoFactory.GetEntryTypeName(e.EntryType) == entryType && BuildMenuEntryDtoFactory.GetEntryKey(e) == entryKey).ToList();
            if (matched.Count == 0) return UniTask.FromResult(ActionResult.Fail("unknown_entry"));

            _buildMenuView.SetSelectedEntry(matched[0]);
            return UniTask.FromResult(ActionResult.Success());
        }
    }

    /// <summary>
    /// blueprint.delete: BPをサーバーから削除する（成功時は OnChanged 経由で一覧が再配信される）
    /// blueprint.delete: deletes a blueprint on the server (success republishes the list via OnChanged)
    /// </summary>
    public class BlueprintDeleteActionHandler : IActionHandler
    {
        public string ActionType => "blueprint.delete";

        private readonly ClientBlueprintLibrary _blueprintLibrary;

        public BlueprintDeleteActionHandler(ClientBlueprintLibrary blueprintLibrary)
        {
            _blueprintLibrary = blueprintLibrary;
        }

        public async UniTask<ActionResult> ExecuteAsync(JObject payload)
        {
            if (payload == null) return ActionResult.Fail("invalid_payload");
            if (payload["name"] is not JValue { Type: JTokenType.String } nameValue) return ActionResult.Fail("invalid_payload");

            await _blueprintLibrary.DeleteBlueprint((string)nameValue, CancellationToken.None);
            return ActionResult.Success();
        }
    }
}
