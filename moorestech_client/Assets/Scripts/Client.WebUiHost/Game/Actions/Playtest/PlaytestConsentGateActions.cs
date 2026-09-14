using Client.WebUiHost.Boot;
using Client.WebUiHost.Game.Playtest;
using Cysharp.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace Client.WebUiHost.Game.Actions.Playtest
{
    /// <summary>
    /// 初回起動の同意表示の了解アクションを Hub へ登録する。
    /// Registers the first-boot consent acknowledgement action with the Hub.
    /// </summary>
    public static class PlaytestConsentGateActions
    {
        public static void Register(WebSocketHub hub, PlaytestConsentGate gate)
        {
            hub.RegisterAction(new AcknowledgePlaytestConsentActionHandler(gate));
        }
    }

    /// <summary>
    /// テスターの了解をゲートへ渡し、判断はゲートに集約する
    /// Hands the tester's acknowledgement to the gate, which owns the judgement
    /// </summary>
    public class AcknowledgePlaytestConsentActionHandler : IActionHandler
    {
        public string ActionType => "playtest.consent.acknowledge";

        private readonly PlaytestConsentGate _gate;

        public AcknowledgePlaytestConsentActionHandler(PlaytestConsentGate gate)
        {
            _gate = gate;
        }

        public UniTask<ActionResult> ExecuteAsync(JObject payload)
        {
            var result = _gate.Acknowledge();
            return UniTask.FromResult(result switch
            {
                PlaytestConsentResult.Acknowledged => ActionResult.Success(),
                // 二度目の了解は何も変えないので成功へ丸めない
                // A second acknowledgement changes nothing, so it is not folded into success
                PlaytestConsentResult.AlreadyAcknowledged => ActionResult.Fail("already_acknowledged"),
                // enumは宣言外の値も取り得る。了解済みと同じコードに相乗りさせると別事象が同じ理由で報告される
                // An enum can hold an undeclared value; sharing the already-acknowledged code would report a different event under the same reason
                _ => ActionResult.Fail("unknown_result"),
            });
        }
    }
}
