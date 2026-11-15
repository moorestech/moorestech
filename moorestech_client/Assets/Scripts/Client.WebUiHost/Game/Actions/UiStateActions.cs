using System;
using Client.Game.InGame.UI.UIState;
using Client.Ui.State.Routing;
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

        private readonly IUiStateTransitionRequester _requester;

        public RequestUiStateActionHandler(IUiStateTransitionRequester requester)
        {
            _requester = requester;
        }

        public UniTask<ActionResult> ExecuteAsync(JObject payload)
        {
            if (payload == null) return UniTask.FromResult(ActionResult.Fail("invalid_payload"));
            if (payload["state"] is not JValue { Type: JTokenType.String } stateValue) return UniTask.FromResult(ActionResult.Fail("invalid_state"));

            // Webから要求できるのは GameScreen / PlayerInventory のみ（SubInventoryは対象ブロックが必要）。遷移の規則は requester が持つ
            // The web may request only GameScreen / PlayerInventory (SubInventory needs a target block); the requester owns the transition rules
            var stateName = (string)stateValue;
            if (stateName != nameof(UIStateEnum.GameScreen) && stateName != nameof(UIStateEnum.PlayerInventory)) return UniTask.FromResult(ActionResult.Fail("unsupported_state"));

            var requested = Enum.Parse<UIStateEnum>(stateName);
            return UniTask.FromResult(_requester.RequestTransition(requested) ? ActionResult.Success() : ActionResult.Fail("transition_not_allowed"));
        }
    }
}
