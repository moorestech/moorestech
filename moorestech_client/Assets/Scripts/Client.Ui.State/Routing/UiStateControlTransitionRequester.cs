using Client.Game.InGame.UI.UIState;
using UnityEngine;

namespace Client.Ui.State.Routing
{
    /// <summary>
    /// 遷移要求を UIStateControl へ渡す本番実装。入れ子ポーズの画面では入れ子だけを閉じ、表に無い遷移は拒む（Web ホストと同じ順）
    /// Production requester forwarding to UIStateControl; on nested-pause screens it closes only the nested pause, and refuses transitions outside the table (same order as the Web host)
    /// </summary>
    public sealed class UiStateControlTransitionRequester : IUiStateTransitionRequester
    {
        private readonly UIStateControl _uiStateControl;

        public UiStateControlTransitionRequester(UIStateControl uiStateControl)
        {
            _uiStateControl = uiStateControl;
        }

        public bool RequestTransition(UIStateEnum requested)
        {
            // 入れ子ポーズを持つ画面の GameScreen 要求は入れ子だけを閉じる（ADR 0035）
            // A GameScreen request on a nested-pause screen closes only the nested pause (ADR 0035)
            if (requested == UIStateEnum.GameScreen && _uiStateControl.GetCurrentNestedPauseScreen() is { } nested) return nested.RequestClosePauseMenu() || Refuse("nested pause had nothing to close");

            if (!UiStateTransitionRules.IsAllowed(_uiStateControl.CurrentState, requested)) return Refuse($"{_uiStateControl.CurrentState} -> {requested} is not allowed");
            _uiStateControl.RequestTransition(requested);
            return true;

            #region Internal

            bool Refuse(string reason)
            {
                Debug.LogWarning($"[UiStateControlTransitionRequester] transition refused: {reason}");
                return false;
            }

            #endregion
        }
    }
}
