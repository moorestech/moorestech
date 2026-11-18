using System;
using Client.Playtest.UiElements.GameUi;

namespace Client.Playtest.UiElements
{
    /// <summary>
    ///     ・シナリオ共通のロケータ保持（UITK のパネルだけを探す）
    ///     ・実行の世代の管理
    ///     ・Shared locator for scenarios (looks up the UITK panel only)
    ///     ・Execution generation tracking
    /// </summary>
    public static class PlaytestUiLocatorSession
    {
        // 切替後は描く系が UITK だけなのでロケータも 1 本
        // After the cutover UITK is the only drawing system, so there is one locator
        public static GameUiElementLocator Locator { get; } = new GameUiElementLocator();
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

        // 成否にかかわらず止め、次回実行へ持ち越さない
        // Stop regardless of outcome and carry nothing into the next execution
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
