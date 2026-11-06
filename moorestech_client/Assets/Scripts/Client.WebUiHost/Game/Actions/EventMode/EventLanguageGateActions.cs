using Client.Ui.State.EventMode;
using Client.WebUiHost.Boot;
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

            // 判定をゲートに委譲し、拒否理由の失敗コード（already_selected・unknown_locale）へ写す
            // Delegate the judgement to the gate and map the reject reason to its failure code (already_selected, unknown_locale)
            return UniTask.FromResult(ActionResult.FromCommandResult(_gate.SelectLanguage(locale)));
        }
    }
}
