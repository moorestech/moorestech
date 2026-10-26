using Client.Playtest.WebUi;

namespace Client.Playtest.UiElements
{
    /// <summary>
    ///     ・シナリオ共通のロケータ保持
    ///     ・照会受け口と入力転送の開閉
    ///     ・Shared locator for scenarios
    ///     ・Query receiver and input forwarding lifecycle
    /// </summary>
    public static class PlaytestUiLocatorSession
    {
        private static readonly WebUiElementLocator WebLocator = new();
        // 段階1はUITK版と両系合成に交換
        // Replace with UITK and the both-renderer composite in stage 1
        public static readonly IUiElementLocator Locator = WebLocator;

        // 冪等。PlaytestRunnerとRun前のprobeの両方から呼ばれる
        // Idempotent; called from both PlaytestRunner and pre-Run probes
        public static void Start()
        {
            WebLocator.StartSession();
        }

        // 成否にかかわらず転送を止め、次回実行へ応答を持ち越さない
        // Stop forwarding and discard pending responses regardless of outcome
        public static void Stop()
        {
            WebLocator.StopSession();
        }
    }
}
