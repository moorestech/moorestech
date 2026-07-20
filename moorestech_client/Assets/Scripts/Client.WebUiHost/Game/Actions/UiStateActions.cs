using System;
using Client.Game.InGame.UI.UIState;
using Client.Game.InGame.UI.UIState.State;
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
        private readonly TrainHUDScreenState _trainHudState;

        public RequestUiStateActionHandler(UIStateControl uiStateControl, TrainHUDScreenState trainHudState)
        {
            _uiStateControl = uiStateControl;
            _trainHudState = trainHudState;
        }

        public UniTask<ActionResult> ExecuteAsync(JObject payload)
        {
            if (payload == null) return UniTask.FromResult(ActionResult.Fail("invalid_payload"));
            if (payload["state"] is not JValue { Type: JTokenType.String } stateValue) return UniTask.FromResult(ActionResult.Fail("invalid_state"));

            // Webから要求できるのは GameScreen / PlayerInventory のみ（SubInventoryは対象ブロックが必要）
            // The web may request only GameScreen / PlayerInventory (SubInventory needs a target block)
            var stateName = (string)stateValue;
            if (stateName != nameof(UIStateEnum.GameScreen) && stateName != nameof(UIStateEnum.PlayerInventory)) return UniTask.FromResult(ActionResult.Fail("unsupported_state"));

            // 乗車中ポーズのGameScreen要求は入れ子だけを閉じ、降車させない
            // A GameScreen request during riding pause closes only the nested pause and never dismounts.
            if (_uiStateControl.CurrentState == UIStateEnum.TrainHUDScreen && stateName == nameof(UIStateEnum.GameScreen))
            {
                _trainHudState.RequestClosePauseMenu();
                return UniTask.FromResult(ActionResult.Success());
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
