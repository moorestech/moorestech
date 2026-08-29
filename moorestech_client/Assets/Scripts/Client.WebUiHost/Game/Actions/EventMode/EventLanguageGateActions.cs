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

            // 判定をゲートに委譲し、全variantを並べた写像で失敗契約へ変換する
            // Delegate the judgement to the gate and map every variant to the failure contract
            var result = _gate.TrySelectLanguage(locale);
            return UniTask.FromResult(result switch
            {
                EventLanguageSelectionResult.Applied => ActionResult.Success(),
                // 二重クリックと再送は言語を変えないので成功へ丸めない
                // A double click or a resend changes no language, so it is not folded into success
                EventLanguageSelectionResult.AlreadySelected => ActionResult.Fail("already_selected"),
                EventLanguageSelectionResult.UnknownLanguage => ActionResult.Fail("unknown_locale"),
                // enumは宣言外の値も取り得るため、未知の選択結果は未知localeと同じ失敗へ倒す
                // An enum can hold an undeclared value, so an unknown outcome falls into the same failure as an unknown locale
                _ => ActionResult.Fail("unknown_locale"),
            });
        }
    }
}
