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

        public RequestUiStateActionHandler(UIStateControl uiStateControl)
        {
            _uiStateControl = uiStateControl;
        }

        public UniTask<ActionResult> ExecuteAsync(JObject payload)
        {
            if (payload == null) return UniTask.FromResult(ActionResult.Fail("invalid_payload"));
            if (payload["state"] is not JValue { Type: JTokenType.String } stateValue) return UniTask.FromResult(ActionResult.Fail("invalid_state"));

            // Webから要求できるのは GameScreen / PlayerInventory のみ（SubInventoryは対象ブロックが必要）
            // The web may request only GameScreen / PlayerInventory (SubInventory needs a target block)
            var stateName = (string)stateValue;
            if (stateName != nameof(UIStateEnum.GameScreen) && stateName != nameof(UIStateEnum.PlayerInventory)) return UniTask.FromResult(ActionResult.Fail("unsupported_state"));

            _uiStateControl.RequestTransition(Enum.Parse<UIStateEnum>(stateName));
            return UniTask.FromResult(ActionResult.Success());
        }
    }
}
