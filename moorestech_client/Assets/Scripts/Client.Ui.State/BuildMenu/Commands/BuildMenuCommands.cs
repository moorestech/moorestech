using System;
using System.Threading;
using Client.Game.InGame.BlockSystem.PlaceSystem.Blueprint;
using Client.Game.InGame.BlockSystem.PlaceSystem.Targets;
using Client.Game.InGame.UI.BuildMenu;
using Client.Game.InGame.UI.UIState;
using Client.Ui.State.Commands;
using Client.Ui.State.Routing;
using Client.Ui.State.Toast;
using Cysharp.Threading.Tasks;

namespace Client.Ui.State.BuildMenu.Commands
{
    /// <summary>
    /// 前提（ビルドメニュー中・解放済み）を確かめて実行する。結果（拒否は失敗コードを持つ理由）はトーストへ渡す
    /// Checks the preconditions (menu open, unlocked) and executes; results (rejections carry reasons with failure codes) go to the toaster
    /// </summary>
    public sealed class BuildMenuCommands : IBuildMenuCommands
    {
        // トーストのログに出す名前は Web の action 名に揃える
        // Names logged with toasts match the Web action names
        public const string SelectCommandName = "build_menu.select";
        public const string DeleteCommandName = "blueprint.delete";

        private readonly UiRoutingState _routing;
        private readonly IUiStateTransitionRequester _transitionRequester;
        private readonly PlacementTargetResolver _placementTargetResolver;
        private readonly BuildMenuSelection _buildMenuSelection;
        private readonly IBlueprintDeleteService _blueprintDeleteService;
        private readonly IUiCommandResultToaster _toaster;

        public BuildMenuCommands(UiRoutingState routing, IUiStateTransitionRequester transitionRequester, PlacementTargetResolver placementTargetResolver, BuildMenuSelection buildMenuSelection, IBlueprintDeleteService blueprintDeleteService, IUiCommandResultToaster toaster)
        {
            _routing = routing;
            _transitionRequester = transitionRequester;
            _placementTargetResolver = placementTargetResolver;
            _buildMenuSelection = buildMenuSelection;
            _blueprintDeleteService = blueprintDeleteService;
            _toaster = toaster;
        }

        public void SelectEntry(Guid targetId)
        {
            _toaster.Report(SelectCommandName, Select());

            #region Internal

            UiCommandResult Select()
            {
                // メニューが先に閉じた・対象が消えた古いクリックは良性の拒否（トーストは Toaster が抑えログだけ残す）
                // Stale clicks after the menu closed or the target vanished are benign rejections (the toaster only logs them)
                if (!IsBuildMenuOpen()) return UiCommandRejected.Of(UiCommandRejectReason.BuildMenuNotOpen);
                if (!_placementTargetResolver.TryResolve(targetId, out var target)) return UiCommandRejected.Of(UiCommandRejectReason.UnknownBuildMenuEntry);
                _buildMenuSelection.SetSelectedTarget(target);
                return UiCommandAccepted.Instance;
            }

            #endregion
        }

        public void DeleteBlueprint(Guid blueprintGuid)
        {
            DeleteAsync().Forget();

            #region Internal

            async UniTaskVoid DeleteAsync()
            {
                // 削除の結果を失敗コードを持つ理由へ写してトーストへ
                // Map the delete result to reasons carrying failure codes and hand it to the toaster
                var result = await _blueprintDeleteService.DeleteBlueprint(blueprintGuid, CancellationToken.None);
                _toaster.Report(DeleteCommandName, ToCommandResult(result));
            }

            #endregion
        }

        public void Close()
        {
            _toaster.Report(UiStateTransitionRules.RequestCommandName, CloseMenu());

            #region Internal

            UiCommandResult CloseMenu()
            {
                // 閉じた後の再送は良性、許可表に拒まれた遷移は閉じられなかった非良性の拒否
                // A resend after close is benign; a transition refused by the allow table is a non-benign close refusal
                if (!IsBuildMenuOpen()) return UiCommandRejected.Of(UiCommandRejectReason.BuildMenuNotOpen);
                if (!_transitionRequester.RequestTransition(UIStateEnum.GameScreen)) return UiCommandRejected.Of(UiCommandRejectReason.ScreenCloseRefused);
                return UiCommandAccepted.Instance;
            }

            #endregion
        }

        private static UiCommandResult ToCommandResult(BlueprintDeleteResult result)
        {
            return result switch
            {
                BlueprintDeleteResult.Success => UiCommandAccepted.Instance,
                BlueprintDeleteResult.NotFound => UiCommandRejected.Of(UiCommandRejectReason.BlueprintDeleteNotFound),
                BlueprintDeleteResult.NotUnlocked => UiCommandRejected.Of(UiCommandRejectReason.BlueprintDeleteNotUnlocked),
                BlueprintDeleteResult.Unknown => UiCommandRejected.Of(UiCommandRejectReason.BlueprintDeleteUnknown),
                BlueprintDeleteResult.RequestFailed => UiCommandRejected.Of(UiCommandRejectReason.BlueprintDeleteRequestFailed),
                _ => throw new ArgumentOutOfRangeException(nameof(result), result, null),
            };
        }

        private bool IsBuildMenuOpen()
        {
            return BuildMenuScreenVisibility.IsShown(_routing.Current.Value);
        }
    }
}
