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
        internal static void Register(WebSocketHub hub, EventLanguageGate gate)
        {
            hub.RegisterAction(new SelectEventLanguageActionHandler(gate));
        }
    }

    /// <summary>
    /// 来場者の選択をゲートへ渡し判断を集約する
    /// Hands the visitor's choice to the gate, which owns the judgement
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

            // 判定をゲートに委譲し失敗契約へ変換
            // Delegate the judgement to the gate and map it to the failure contract
            var result = _gate.TrySelectLanguage(locale);
            return UniTask.FromResult(result == EventLanguageSelectionResult.UnknownLanguage
                ? ActionResult.Fail("unknown_locale")
                : ActionResult.Success());
        }
    }
}
