using Client.WebUiHost.Boot;
using Client.WebUiHost.Game.Playtest;
using Cysharp.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace Client.WebUiHost.Game.Actions.Playtest
{
    /// <summary>
    /// 前回異常終了の応答アクションを Hub へ登録する。
    /// Registers the previous-crash answer action with the Hub.
    /// </summary>
    public static class CrashReportGateActions
    {
        public static void Register(WebSocketHub hub, CrashReportGate gate)
        {
            hub.RegisterAction(new CrashReportRespondActionHandler(gate));
        }
    }

    /// <summary>
    /// テスターの応答をゲートへ渡し、判断はゲートに集約する
    /// Hands the tester's answer to the gate, which owns the judgement
    /// </summary>
    public class CrashReportRespondActionHandler : IActionHandler
    {
        public string ActionType => "playtest.crash_report.respond";

        private readonly CrashReportGate _gate;

        public CrashReportRespondActionHandler(CrashReportGate gate)
        {
            _gate = gate;
        }

        public UniTask<ActionResult> ExecuteAsync(JObject payload)
        {
            var send = payload?["send"]?.Value<bool>() ?? false;
            var description = payload?["description"]?.ToString() ?? "";

            // 判定をゲートに委譲し、全variantを並べた写像で失敗契約へ変換する
            // Delegate the judgement to the gate and map every variant to the failure contract
            var result = _gate.Respond(send, description);
            return UniTask.FromResult(result switch
            {
                CrashReportResponseResult.Sent => ActionResult.Success(),
                CrashReportResponseResult.Skipped => ActionResult.Success(),
                // 二度目の応答は何も変えないので成功へ丸めない
                // A second answer changes nothing, so it is not folded into success
                CrashReportResponseResult.AlreadyResponded => ActionResult.Fail("already_responded"),
                // enumは宣言外の値も取り得るため、未知の結果は応答済みと同じ失敗へ倒す
                // An enum can hold an undeclared value, so an unknown outcome falls into the same failure as an already-answered one
                _ => ActionResult.Fail("already_responded"),
            });
        }
    }
}
