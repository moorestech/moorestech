using Client.WebUiHost.Boot;
using Client.WebUiHost.Game.EventMode;
using Cysharp.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace Client.WebUiHost.Game.Actions.EventMode
{
    /// <summary>
    /// 出展モードの言語選択アクションを Hub へ登録する。
    /// Registers the event-mode language selection action with the Hub.
    /// </summary>
    public static class EventLanguageGateActions
    {
        public static void Register(WebSocketHub hub, EventLanguageGate gate)
        {
            hub.RegisterAction(new SelectEventLanguageActionHandler(gate));
        }
    }

    /// <summary>
    /// 来場者の言語選択をゲートへ渡し、可否の判断はゲートへ集約する。
    /// Hands the visitor's choice to the gate, which owns the accept/reject judgement.
    /// </summary>
    public class SelectEventLanguageActionHandler : IActionHandler
    {
        public string ActionType => "event_mode.select_language";

        private readonly EventLanguageGate _gate;

        public SelectEventLanguageActionHandler(EventLanguageGate gate)
        {
            _gate = gate;
        }

        public UniTask<ActionResult> ExecuteAsync(JObject payload)
        {
            var locale = payload?["locale"]?.ToString();

            // 選択可否の判定はゲート側に集約し、結果を失敗契約へ写す
            // Delegate the selectability judgement to the gate and map the result to the failure contract
            return UniTask.FromResult(_gate.TrySelectLanguage(locale)
                ? ActionResult.Success()
                : ActionResult.Fail("unknown_locale"));
        }
    }
}
