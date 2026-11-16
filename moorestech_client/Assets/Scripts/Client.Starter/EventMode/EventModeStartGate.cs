using Client.Ui.State.EventMode;
using System.Threading;
using Client.Input;
using Cysharp.Threading.Tasks;

namespace Client.Starter.EventMode
{
    /// <summary>
    /// 出展モードの開始ゲート。言語が選ばれるまで開始を止め、選択と同時に無操作監視を始める。
    /// The event-mode start gate: holds the start until a language is chosen and begins idle watching at that moment.
    /// </summary>
    public static class EventModeStartGate
    {
        // 描く系は UITK だけなので、MainGameStarter が DI より前に GameUiRoot の層へ用意した画面で言語ゲートを出して待つ
        // UITK is the only drawing system, so show the language gate on the screen MainGameStarter prepared on a GameUiRoot layer before DI, and wait
        public static UniTask WaitForLanguageSelectionAsync(IEventLanguageGateScreen screen, CancellationToken ct)
        {
            return WaitOnGameUiAsync(EventExhibitionSettings.FromEnvironment(), screen, new EventIdleQuitWatcherArmer(), ct);
        }

        // UITK のゲートの待ち。出す前提（層のパネル・言語の一覧）は土台の Initialize と生成時のビルドが保証するので、英語開始の縮退は無い（S5-gate D-3）
        // The UITK gate's wait; the foundation's Initialize and the generator's build guarantee the preconditions (layer panel, language list), so there is no English fallback (S5-gate D-3)
        internal static async UniTask WaitOnGameUiAsync(EventExhibitionSettings settings, IEventLanguageGateScreen screen, IEventIdleWatchArmer armer, CancellationToken ct)
        {
            // silent-ok: 通常の起動に言語ゲートは無い / 出展モードで起動したのにゲートが出なければ来場者と録画（uitk-event-language-gate）がその場で気づく
            if (!settings.IsEnabled) return;

            var gate = new EventLanguageGate(true);
            screen.Open(gate);

            ShowCursorAfterSceneStartAsync(ct).Forget();
            await AwaitSelectionThenArmAsync(gate, settings.IdleTimeoutSeconds, armer, ct);
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

            // 選択の継続は UITK のクリックの処理の中で走る。ここで手放して初期化をクリックの外で進める
            // The continuation resumes inside the UITK click handling, so yield here to run initialization outside the click
            await UniTask.Yield();

            // 武装は選択より後にしか起こらない。待機中は監視個体が存在しないので無操作終了は起こり得ない
            // Arming can only happen after the selection; no watcher exists while waiting, so an idle quit cannot fire
            armer.ArmIdleWatch(idleTimeoutSeconds);
        }
    }
}
