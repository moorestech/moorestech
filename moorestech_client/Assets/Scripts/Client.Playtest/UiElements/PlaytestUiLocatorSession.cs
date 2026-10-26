using Client.Playtest.WebUi;

namespace Client.Playtest.UiElements
{
    /// <summary>
    ///     シナリオが使うロケータを1か所で持ち、照会の受け口と入力転送を開閉する
    ///     Holds the locator used by scenarios in one place and opens/closes query receivers and input forwarding
    /// </summary>
    public static class PlaytestUiLocatorSession
    {
        private static readonly WebUiElementLocator WebLocator = new();
        // 段階1でUITK版と両系照合の合成へ差し替える
        // Replaced by the UITK + both-renderer composite in stage 1
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
