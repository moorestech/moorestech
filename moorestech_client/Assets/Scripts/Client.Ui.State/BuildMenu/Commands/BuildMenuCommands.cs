using System;
using System.Threading;
using Client.Game.InGame.BlockSystem.PlaceSystem.Blueprint;
using Client.Game.InGame.BlockSystem.PlaceSystem.Targets;
using Client.Game.InGame.UI.BuildMenu;
using Client.Game.InGame.UI.UIState;
using Client.Ui.State.Commands;
using Client.Ui.State.Routing;
using Cysharp.Threading.Tasks;

namespace Client.Ui.State.BuildMenu.Commands
{
    /// <summary>
    /// 前提（ビルドメニュー中・解放済み）を確かめて実行する。拒否は Web の橋と同じ失敗コードの理由で返す
    /// Checks the preconditions (menu open, unlocked) and executes; rejections carry reasons with the Web bridge failure codes
    /// </summary>
    public sealed class BuildMenuCommands : IBuildMenuCommands
    {
        private readonly UIStateControl _uiStateControl;
        private readonly IUiStateTransitionRequester _transitionRequester;
        private readonly PlacementTargetResolver _placementTargetResolver;
        private readonly BuildMenuSelection _buildMenuSelection;
        private readonly IBlueprintDeleteService _blueprintDeleteService;

        public BuildMenuCommands(UIStateControl uiStateControl, IUiStateTransitionRequester transitionRequester, PlacementTargetResolver placementTargetResolver, BuildMenuSelection buildMenuSelection, IBlueprintDeleteService blueprintDeleteService)
        {
            _uiStateControl = uiStateControl;
            _transitionRequester = transitionRequester;
            _placementTargetResolver = placementTargetResolver;
            _buildMenuSelection = buildMenuSelection;
            _blueprintDeleteService = blueprintDeleteService;
        }

        public UiCommandResult SelectEntry(Guid targetId)
        {
            // メニューが先に閉じた・対象が消えた古いクリックは良性の拒否（トーストは Toaster が抑えログだけ残す）
            // Stale clicks after the menu closed or the target vanished are benign rejections (the toaster only logs them)
            if (!IsBuildMenuOpen()) return new UiCommandRejected(UiCommandRejectReason.BuildMenuNotOpen, "");
            if (!_placementTargetResolver.TryResolve(targetId, out var target)) return new UiCommandRejected(UiCommandRejectReason.UnknownBuildMenuEntry, "");
            _buildMenuSelection.SetSelectedTarget(target);
            return UiCommandAccepted.Instance;
        }

        public async UniTask<UiCommandResult> DeleteBlueprint(Guid blueprintGuid)
        {
            // 削除の結果を Web の橋と同じ失敗コードの理由へ写す
            // Map the delete result to reasons carrying the Web bridge failure codes
            var result = await _blueprintDeleteService.DeleteBlueprint(blueprintGuid, CancellationToken.None);
            return result switch
            {
                BlueprintDeleteResult.Success => UiCommandAccepted.Instance,
                BlueprintDeleteResult.NotFound => new UiCommandRejected(UiCommandRejectReason.BlueprintDeleteNotFound, ""),
                BlueprintDeleteResult.NotUnlocked => new UiCommandRejected(UiCommandRejectReason.BlueprintDeleteNotUnlocked, ""),
                BlueprintDeleteResult.Unknown => new UiCommandRejected(UiCommandRejectReason.BlueprintDeleteUnknown, ""),
                BlueprintDeleteResult.RequestFailed => new UiCommandRejected(UiCommandRejectReason.BlueprintDeleteRequestFailed, ""),
                _ => throw new ArgumentOutOfRangeException(nameof(result), result, null),
            };
        }

        public UiCommandResult Close()
        {
            // 閉じた後の再送と許可表に拒まれた遷移は、どちらもメニューが開いていない扱いの良性の拒否
            // A resend after close and a transition refused by the allow table are both benign "menu not open" rejections
            if (!IsBuildMenuOpen()) return new UiCommandRejected(UiCommandRejectReason.BuildMenuNotOpen, "");
            if (!_transitionRequester.RequestTransition(UIStateEnum.GameScreen)) return new UiCommandRejected(UiCommandRejectReason.BuildMenuNotOpen, "");
            return UiCommandAccepted.Instance;
        }

        private bool IsBuildMenuOpen()
        {
            return _uiStateControl.CurrentState == UIStateEnum.BuildMenu;
        }
    }
}
