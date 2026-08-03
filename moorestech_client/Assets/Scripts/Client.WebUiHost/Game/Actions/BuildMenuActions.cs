using System;
using System.Threading;
using Client.Game.InGame.BlockSystem.PlaceSystem.Blueprint;
using Client.Game.InGame.BlockSystem.PlaceSystem.Targets;
using Client.Game.InGame.UI.BuildMenu;
using Client.Game.InGame.UI.UIState;
using Common.Debug;
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
        private readonly PlacementTargetCatalog _placementTargetCatalog;
        private readonly BuildMenuView _buildMenuView;

        public BuildMenuSelectActionHandler(UIStateControl uiStateControl, IGameUnlockStateData unlockState, ClientBlueprintLibrary blueprintLibrary, PlacementTargetCatalog placementTargetCatalog, BuildMenuView buildMenuView)
        {
            _uiStateControl = uiStateControl;
            _unlockState = unlockState;
            _blueprintLibrary = blueprintLibrary;
            _placementTargetCatalog = placementTargetCatalog;
            _buildMenuView = buildMenuView;
        }

        public UniTask<ActionResult> ExecuteAsync(JObject payload)
        {
            if (payload == null) return UniTask.FromResult(ActionResult.Fail("invalid_payload"));
            if (payload["id"] is not JValue { Type: JTokenType.String } idValue) return UniTask.FromResult(ActionResult.Fail("invalid_payload"));
            if (!Guid.TryParse((string)idValue, out var targetId)) return UniTask.FromResult(ActionResult.Fail("invalid_payload"));

            // BuildMenu以外でのstale到達（Unity側が先に閉じたレース）は拒否する
            // Reject stale arrivals outside BuildMenu (the Unity side closed the menu first)
            if (_uiStateControl.CurrentState != UIStateEnum.BuildMenu) return UniTask.FromResult(ActionResult.Fail("invalid_state"));

            // 現在のカタログ（未解放を除外済み）とIdで照合し、削除済みBP等へのstaleクリックを弾く
            // Match by id against the current catalog (locked entries already excluded), rejecting stale clicks such as deleted blueprints
            // Idはカタログ内で一意のため最初の一致で足りる（複数一致はそもそも起こり得ない）
            // Id is unique within the catalog, so the first match suffices (multiple matches can never occur)
            var showAllPlaceable = DebugParameters.GetValueOrDefaultBool(DebugParameterKeys.FreeBlockPlacement);
            IPlacementTarget target = null;
            foreach (var entry in _placementTargetCatalog.UnlockedEntries(_unlockState, showAllPlaceable, _blueprintLibrary.BlueprintEntries))
            {
                if (entry.Id != targetId) continue;
                target = PlacementTargetFactory.Create(entry);
                break;
            }
            if (target == null) return UniTask.FromResult(ActionResult.Fail("unknown_entry"));

            // uGUIの消費キューへはターゲットのみ渡す（uGUI表示は使われない）
            // Feed only the target into the uGUI consume queue (its visual display is unused)
            _buildMenuView.SetSelectedEntry(new BuildMenuEntry(target, null, target.DisplayName));
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
            if (payload["id"] is not JValue { Type: JTokenType.String } idValue) return ActionResult.Fail("invalid_payload");
            if (!Guid.TryParse((string)idValue, out var blueprintGuid)) return ActionResult.Fail("invalid_payload");

            // NotFound（二重右クリック等のstale削除）と通信失敗はコードを分け、前者のみwebui側でトースト抑止する
            // Split NotFound (stale deletes e.g. double right-click) from communication failure so only the former is toast-suppressed on the webui side
            var result = await _blueprintLibrary.DeleteBlueprint(blueprintGuid, CancellationToken.None);
            return result switch
            {
                BlueprintDeleteResult.Success => ActionResult.Success(),
                BlueprintDeleteResult.NotFound => ActionResult.Fail("blueprint_delete_not_found"),
                _ => ActionResult.Fail("blueprint_delete_request_failed"),
            };
        }
    }
}
