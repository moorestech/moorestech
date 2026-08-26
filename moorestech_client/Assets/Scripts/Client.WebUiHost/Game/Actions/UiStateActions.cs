using System;
using Client.Game.InGame.UI.UIState;
using Cysharp.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace Client.WebUiHost.Game.Actions
{
    /// <summary>
    /// ui_state.request: Web からのUIState遷移要求を UIStateControl に渡す
    /// ui_state.request: forwards a UI-state transition request from the web to UIStateControl
    /// </summary>
    public class RequestUiStateActionHandler : IActionHandler
    {
        public string ActionType => "ui_state.request";

        private readonly UIStateControl _uiStateControl;
        private readonly UIStateDictionary _uiStateDictionary;

        public RequestUiStateActionHandler(UIStateControl uiStateControl, UIStateDictionary uiStateDictionary)
        {
            _uiStateControl = uiStateControl;
            _uiStateDictionary = uiStateDictionary;
        }

        public UniTask<ActionResult> ExecuteAsync(JObject payload)
        {
            if (payload == null) return UniTask.FromResult(ActionResult.Fail("invalid_payload"));
            if (payload["state"] is not JValue { Type: JTokenType.String } stateValue) return UniTask.FromResult(ActionResult.Fail("invalid_state"));

            // Webから要求できるのは GameScreen / PlayerInventory のみ（SubInventoryは対象ブロックが必要）
            // The web may request only GameScreen / PlayerInventory (SubInventory needs a target block)
            var stateName = (string)stateValue;
            if (stateName != nameof(UIStateEnum.GameScreen) && stateName != nameof(UIStateEnum.PlayerInventory)) return UniTask.FromResult(ActionResult.Fail("unsupported_state"));

            // 入れ子ポーズを持つ画面のGameScreen要求は、その入れ子だけを閉じて画面自体は維持する（ADR 0035）
            // A GameScreen request on a nested-pause screen closes only that nested pause and keeps the screen itself (ADR 0035)
            if (stateName == nameof(UIStateEnum.GameScreen) && _uiStateDictionary.GetState(_uiStateControl.CurrentState) is INestedPauseScreenState nestedScreen)
            {
                // 閉じるものが無い要求は成功に見せず拒否する
                // A request with nothing to close is rejected instead of reported as success
                var closed = nestedScreen.RequestClosePauseMenu();
                return UniTask.FromResult(closed ? ActionResult.Success() : ActionResult.Fail("transition_not_allowed"));
            }

            var requested = Enum.Parse<UIStateEnum>(stateName);
            if (!IsAllowed(_uiStateControl.CurrentState, requested)) return UniTask.FromResult(ActionResult.Fail("transition_not_allowed"));
            _uiStateControl.RequestTransition(requested);
            return UniTask.FromResult(ActionResult.Success());
        }

        public static bool IsAllowed(UIStateEnum current, UIStateEnum requested)
        {
            if (current == requested) return true;
            return current switch
            {
                UIStateEnum.GameScreen => requested == UIStateEnum.PlayerInventory,
                UIStateEnum.PlayerInventory => requested == UIStateEnum.GameScreen,
                UIStateEnum.SubInventory => requested == UIStateEnum.GameScreen,
                UIStateEnum.BuildMenu => requested == UIStateEnum.GameScreen,
                // C1/C2で追加されたWeb画面の閉じ操作。Story/PauseMenu中の強制遷移は引き続き拒否
                // Close paths for the C1/C2 web screens; forced transitions during Story/PauseMenu stay rejected
                UIStateEnum.ResearchTree => requested == UIStateEnum.GameScreen,
                UIStateEnum.ChallengeList => requested == UIStateEnum.GameScreen,
                UIStateEnum.PauseMenu => requested == UIStateEnum.GameScreen,
                _ => false,
            };
        }
    }
}
