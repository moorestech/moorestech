using System.Threading;
using Client.WebUiHost.Boot;
using Client.WebUiHost.Game.EventMode;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Client.Starter.EventMode
{
    /// <summary>
    /// 出展モードの開始ゲート。言語が選ばれるまで開始を止め、選択と同時に無操作監視を始める。
    /// The event-mode start gate: holds the start until a language is chosen and begins idle watching at that moment.
    /// </summary>
    public static class EventModeStartGate
    {
        public static UniTask WaitForLanguageSelectionAsync(CancellationToken ct)
        {
            return WaitForLanguageSelectionWithHubAsync(Client.WebUiHost.Boot.WebUiHost.Hub, EventExhibitionSettings.FromEnvironment(), ct);
        }

        // hubと設定を引数で受ける本体。Kestrelを立てずに通常モードの無条件登録を検証できるよう分ける（PlaytestStartGatesと同形）
        // The body takes the hub and settings so the unconditional registration in normal mode is verifiable without Kestrel (same shape as PlaytestStartGates)
        internal static async UniTask WaitForLanguageSelectionWithHubAsync(WebSocketHub hub, EventExhibitionSettings settings, CancellationToken ct)
        {
            // topicの登録は出展モードか否かに関わらず無条件に行う。条件付き登録だとWeb側の購読が固着する
            // Registration happens unconditionally regardless of exhibition mode; conditional registration would wedge the web-side subscription
            EventLanguageGate gate = null;
            if (hub != null) gate = EventLanguageGateBinder.Bind(hub, settings.IsEnabled);

            if (!settings.IsEnabled) return;

            var armer = new EventIdleQuitWatcherArmer();

            // 画面を出せないなら無人ブースを止めない方を採る。英語のまま開始し監視だけ始める
            // With no screen to show, keeping the unattended booth alive wins: start in English and only begin watching
            if (hub == null)
            {
                Debug.LogError("EventModeStartGate: WebUiHostが起動しておらず言語選択を出せないため英語のまま開始します");
                armer.ArmIdleWatch(settings.IdleTimeoutSeconds);
                return;
            }

            await AwaitSelectionThenArmAsync(gate, settings.IdleTimeoutSeconds, armer, ct);
        }

        // 「選択を待ってから武装する」順序がこのゲートの契約そのものなので、順序だけを切り出して押さえる
        // The wait-then-arm order is this gate's contract itself, so the order alone is split out to be pinned by tests
        // 待ちは上限を持たないため、終了のキャンセルが来たら来場者の選択を待たずに抜ける
        // The wait is unbounded, so an exit cancellation leaves without waiting for the visitor's choice
        internal static async UniTask AwaitSelectionThenArmAsync(EventLanguageGate gate, int idleTimeoutSeconds, IEventIdleWatchArmer armer, CancellationToken ct)
        {
            await gate.WaitForSelectionAsync().AttachExternalCancellation(ct);

            // 選択の継続はaction処理スタックの中で走る。ここで手放さないと初期化の間WSの受信ループが止まる
            // The continuation resumes inside the action's stack, so yielding here keeps the WS receive loop alive during initialization
            await UniTask.Yield();

            // 武装は選択より後にしか起こらない。待機中は監視個体が存在しないので無操作終了は起こり得ない
            // Arming can only happen after the selection; no watcher exists while waiting, so an idle quit cannot fire
            armer.ArmIdleWatch(idleTimeoutSeconds);
        }
    }
}
