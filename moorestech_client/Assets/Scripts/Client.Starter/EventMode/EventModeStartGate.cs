using Client.Ui.State.EventMode;
using System.Threading;
using Client.Input;
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
        // 系の出し分けはここ 1 か所。系ごとの待ちは、それぞれの描き手（GameUi は画面の境界、Web は hub）を必須で受ける
        // The UI-system dispatch lives only here; each system's wait takes its own drawer as required (GameUi the screen seam, Web the hub)
        public static UniTask WaitForLanguageSelectionAsync(MainGameStarter starter, CancellationToken ct)
        {
            var settings = EventExhibitionSettings.FromEnvironment();
            var armer = new EventIdleQuitWatcherArmer();

            // GameUi の系は hub を見ず UITK ゲートを出す
            // GameUi system skips the hub and shows the UITK gate
            if (UiInputExclusivity.ActiveUiSystem == UiSystem.GameUi) return WaitOnGameUiAsync(settings, starter.CreateEventLanguageGateScreen(), armer, ct);

            // Web の系で Web ホストが立っていなければ画面を出せない（起動の失敗はWebUiStartupがLogError済み）
            // In the Web system a missing web host means no screen (WebUiStartup has already logged the startup failure)
            var hub = Client.WebUiHost.Boot.WebUiHost.Hub;
            if (hub == null)
            {
                StartInEnglishWithoutWebHost(settings, armer);
                return UniTask.CompletedTask;
            }
            return WaitOnWebAsync(hub, settings, armer, ct);
        }

        // GameUi の系の待ち。出す前提（層のパネル・言語の一覧）は土台の Initialize と生成時のビルドが保証するので、英語開始の縮退は無い（S5-gate D-3）
        // The GameUi system's wait; the foundation's Initialize and the generator's build guarantee the preconditions (layer panel, language list), so there is no English fallback (S5-gate D-3)
        internal static async UniTask WaitOnGameUiAsync(EventExhibitionSettings settings, IEventLanguageGateScreen screen, IEventIdleWatchArmer armer, CancellationToken ct)
        {
            // silent-ok: 通常の起動に言語ゲートは無い / 出展モードで起動したのにゲートが出なければ来場者と録画（uitk-event-language-gate）がその場で気づく
            if (!settings.IsEnabled) return;

            var gate = new EventLanguageGate(true);
            screen.Open(gate);

            ShowCursorAfterSceneStartAsync(ct).Forget();
            await AwaitSelectionThenArmAsync(gate, settings.IdleTimeoutSeconds, armer, ct);
        }

        // Web の系の待ち。hubを引数で受け、Kestrelを立てずに通常モードの無条件登録を検証できるよう分ける（PlaytestStartGatesと同形）
        // The Web system's wait; takes the hub so the unconditional registration in normal mode is verifiable without Kestrel (same shape as PlaytestStartGates)
        internal static async UniTask WaitOnWebAsync(WebSocketHub hub, EventExhibitionSettings settings, IEventIdleWatchArmer armer, CancellationToken ct)
        {
            // topicの登録は出展モードか否かに関わらず無条件に行う。条件付き登録だとWeb側の購読が固着する
            // Registration happens unconditionally regardless of exhibition mode; conditional registration would wedge the web-side subscription
            var gate = EventLanguageGateBinder.Bind(hub, settings.IsEnabled);

            if (!settings.IsEnabled) return;

            // 来場者が言語ボタンを押せるよう待機中は実カーソルを出す。選択後のロックはGameScreenStateの入場が担う
            // Show the real cursor while waiting so visitors can press a language button; GameScreenState re-locks it after the choice
            ShowCursorAfterSceneStartAsync(ct).Forget();

            await AwaitSelectionThenArmAsync(gate, settings.IdleTimeoutSeconds, armer, ct);
        }

        // Web の系で Web ホストが立っていないとき。画面を出せないなら無人ブースを止めない方を採り、英語のまま開始し監視だけ始める
        // The Web system without a web host: with no screen to show, keeping the unattended booth alive wins, so start in English and only begin watching
        internal static void StartInEnglishWithoutWebHost(EventExhibitionSettings settings, IEventIdleWatchArmer armer)
        {
            // silent-ok: 通常の起動に言語ゲートは無い / Web ホストの起動の失敗は WebUiStartup が LogError 済み
            if (!settings.IsEnabled) return;

            Debug.LogError("EventModeStartGate: WebUiHostが起動しておらず言語選択を出せないため英語のまま開始します");
            armer.ArmIdleWatch(settings.IdleTimeoutSeconds);
        }

        // 待機開始はsceneLoaded内でGameStateController.Start()より先に走り、Startがカーソルをロックする。1フレーム待って後勝ちにする
        // The wait starts inside sceneLoaded, before GameStateController.Start() locks the cursor, so wait one frame to win last
        private static async UniTask ShowCursorAfterSceneStartAsync(CancellationToken ct)
        {
            await UniTask.NextFrame(ct);
            InputManager.MouseCursorVisible(true);
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
