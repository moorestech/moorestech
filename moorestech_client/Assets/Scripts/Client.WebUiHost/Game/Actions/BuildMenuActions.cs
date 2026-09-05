using System;
using System.Threading;
using Client.Game.InGame.BlockSystem.PlaceSystem.Blueprint;
using Client.Game.InGame.BlockSystem.PlaceSystem.Targets;
using Client.Game.InGame.UI.BuildMenu;
using Client.Game.InGame.UI.UIState;
using Cysharp.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace Client.WebUiHost.Game.Actions
{
    /// <summary>
    /// build_menu.select: web の選択を BuildMenuState の消費キューへ投入する
    /// build_menu.select: feeds a web selection into BuildMenuState's consume queue
    /// </summary>
    public class BuildMenuSelectActionHandler : IActionHandler
    {
        public string ActionType => "build_menu.select";

        private readonly UIStateControl _uiStateControl;
        private readonly PlacementTargetResolver _placementTargetResolver;
        private readonly BuildMenuSelection _buildMenuSelection;

        public BuildMenuSelectActionHandler(UIStateControl uiStateControl, PlacementTargetResolver placementTargetResolver, BuildMenuSelection buildMenuSelection)
        {
            _uiStateControl = uiStateControl;
            _placementTargetResolver = placementTargetResolver;
            _buildMenuSelection = buildMenuSelection;
        }

        public UniTask<ActionResult> ExecuteAsync(JObject payload)
        {
            if (payload == null) return UniTask.FromResult(ActionResult.Fail("invalid_payload"));
            if (payload["id"] is not JValue { Type: JTokenType.String } idValue) return UniTask.FromResult(ActionResult.Fail("invalid_payload"));
            if (!Guid.TryParse((string)idValue, out var targetId)) return UniTask.FromResult(ActionResult.Fail("invalid_payload"));

            // BuildMenu以外でのstale到達（Unity側が先に閉じたレース）は拒否する
            // Reject stale arrivals outside BuildMenu (the Unity side closed the menu first)
            if (_uiStateControl.CurrentState != UIStateEnum.BuildMenu) return UniTask.FromResult(ActionResult.Fail("invalid_state"));

            // 現在解決できる設置対象（未解放を除外済み）とIdで照合し、削除済みBP等へのstaleクリックを弾く
            // Match by id against the currently resolvable targets (locked entries already excluded), rejecting stale clicks such as deleted blueprints
            if (!_placementTargetResolver.TryResolve(targetId, out var target)) return UniTask.FromResult(ActionResult.Fail("unknown_entry"));

            // BuildMenuState の消費キューへ渡す
            // Hand the target to BuildMenuState's consume queue
            _buildMenuSelection.SetSelectedTarget(target);
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

        private readonly IBlueprintDeleteService _blueprintDeleteService;

        public BlueprintDeleteActionHandler(IBlueprintDeleteService blueprintDeleteService)
        {
            _blueprintDeleteService = blueprintDeleteService;
        }

        public async UniTask<ActionResult> ExecuteAsync(JObject payload)
        {
            if (payload == null) return ActionResult.Fail("invalid_payload");
            if (payload["id"] is not JValue { Type: JTokenType.String } idValue) return ActionResult.Fail("invalid_payload");
            if (!Guid.TryParse((string)idValue, out var blueprintGuid)) return ActionResult.Fail("invalid_payload");

            // NotFound（二重右クリック等のstale削除）と通信失敗はコードを分け、前者のみwebui側でトースト抑止する
            // Split NotFound (stale deletes e.g. double right-click) from communication failure so only the former is toast-suppressed on the webui side
            var result = await _blueprintDeleteService.DeleteBlueprint(blueprintGuid, CancellationToken.None);
            return result switch
            {
                BlueprintDeleteResult.Success => ActionResult.Success(),
                BlueprintDeleteResult.NotFound => ActionResult.Fail("blueprint_delete_not_found"),
                BlueprintDeleteResult.NotUnlocked => ActionResult.Fail("blueprint_delete_not_unlocked"),
                _ => ActionResult.Fail("blueprint_delete_request_failed"),
            };
        }
    }
}
