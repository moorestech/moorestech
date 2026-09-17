using Client.WebUiHost.Boot;
using Client.WebUiHost.Game.Playtest;
using Cysharp.Threading.Tasks;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace Client.WebUiHost.Game.Actions.Playtest
{
    /// <summary>
    /// 前回異常終了の応答アクションを Hub へ登録する。
    /// Registers the previous-crash answer action with the Hub.
    /// </summary>
    internal static class CrashReportGateActions
    {
        internal static void Register(WebSocketHub hub, CrashReportGate gate)
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

        public async UniTask<ActionResult> ExecuteAsync(JObject payload)
        {
            // 送るかどうかはテスターの明示回答。欠けた・boolでない要求を「送らない」へ丸めると答えを捏造する
            // Sending is the tester's explicit answer; folding a missing or non-bool value into "do not send" fabricates the answer
            if (payload?["send"] is not JValue { Type: JTokenType.Boolean } sendToken)
            {
                Debug.LogWarning($"前回異常終了の応答に bool の send が無いため拒否します send:{payload?["send"]?.Type.ToString() ?? "missing"}");
                return ActionResult.Fail("invalid_send");
            }
            var send = sendToken.Value<bool>();
            var description = payload?["description"]?.ToString() ?? "";

            // 判定をゲートに委譲し、全variantを並べた写像で失敗契約へ変換する
            // Delegate the judgement to the gate and map every variant to the failure contract
            var result = await _gate.RespondAsync(send, description);
            return result switch
            {
                CrashReportResponseResult.Sent => ActionResult.Success(),
                CrashReportResponseResult.Skipped => ActionResult.Success(),
                // 箱を書けなかった送信は成功にしない。理由コードはポーズメニュー経路の書き出し失敗と同じものを使う
                // A send whose box could not be written is never a success; the reason code matches the pause-menu write failure
                CrashReportResponseResult.WriteFailed => ActionResult.Fail("bundle_write_failed"),
                // 二度目の応答は何も変えないので成功へ丸めない
                // A second answer changes nothing, so it is not folded into success
                CrashReportResponseResult.AlreadyResponded => ActionResult.Fail("already_responded"),
                // enumは宣言外の値も取り得る。応答済みと同じコードに相乗りさせると別事象が同じ理由で報告される
                // An enum can hold an undeclared value; sharing the already-answered code would report a different event under the same reason
                _ => ActionResult.Fail("unknown_result"),
            };
        }
    }
}
