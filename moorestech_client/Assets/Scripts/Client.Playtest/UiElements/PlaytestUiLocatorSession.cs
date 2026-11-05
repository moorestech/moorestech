using System;
using Client.Playtest.UiElements.GameUi;
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
        // GameUiを先に探し、両系で見つかれば失敗させる
        // Prefer GameUi and fail when both renderers resolve the testid
        public static readonly IUiElementLocator Locator = new CompositeUiElementLocator(new GameUiElementLocator(), new WebUiElementLocator());
        private static int _generation;
        private static bool _active;

        // 冪等。PlaytestRunnerとRun前のprobeの両方から呼ばれる
        // Idempotent; called from both PlaytestRunner and pre-Run probes
        public static void Start()
        {
            if (!_active) _generation++;
            _active = true;
            Locator.StartSession();
        }

        // 成否にかかわらず転送を止め、次回実行へ応答を持ち越さない
        // Stop forwarding and discard pending responses regardless of outcome
        public static void Stop()
        {
            _active = false;
            Locator.StopSession();
        }
        // 開始した実行だけに再送を許す
        // Permit retries only from the execution that started the operation
        internal static int CaptureGeneration()
        {
            RequireGeneration(_generation);
            return _generation;
        }

        internal static void RequireGeneration(int generation)
        {
            if (!_active || generation != _generation)
                throw new InvalidOperationException("UI operation belongs to a stopped session");
        }
    }
}
