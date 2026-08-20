using Client.Game.InGame.UI.UIState;
using Client.Playtest.Core;
using Client.Playtest.Operations;
using Cysharp.Threading.Tasks;

namespace Client.Playtest
{
    /// <summary>
    ///     ホットバー操作のサブファサード
    ///     Hotbar operation sub-facade, called from scenarios via PlaytestDriver.Hotbar
    /// </summary>
    public class PlaytestHotbarDriver
    {
        // 建築モード出入りの遷移待ち上限
        // The transition timeout for entering and leaving build mode
        private const float UiStateTimeoutSeconds = 10f;

        private readonly PlaytestReporter _reporter;

        public PlaytestHotbarDriver(PlaytestReporter reporter)
        {
            _reporter = reporter;
        }

        /// <summary>
        ///     slotは0始まり(0→キー1)。割当済み対象を持って建築モードへ入り、遷移完了まで待つ
        ///     slot is zero-based (0 -> key "1"); enters build mode holding the assigned target and waits for the transition
        /// </summary>
        public async UniTask EnterBuildMode(int slot) => await _reporter.Act($"ホットバー{slot + 1}で建築モードへ", () => PlaytestHotbarOps.TapSlotAndWaitUiState(slot, UIStateEnum.PlaceBlock, UiStateTimeoutSeconds));

        /// <summary>
        ///     入場と同じ枠をもう一度叩いて建築モードを抜ける。持ち替えではなくトグルなので枠番号は入場時と一致させる
        ///     Taps the entry slot again to leave build mode; it is a toggle rather than a swap, so the slot must match the one used to enter
        /// </summary>
        public async UniTask ExitBuildMode(int slot) => await _reporter.Act($"ホットバー{slot + 1}で建築モードを抜ける", () => PlaytestHotbarOps.TapSlotAndWaitUiState(slot, UIStateEnum.GameScreen, UiStateTimeoutSeconds));

        // ビルドメニューと同一供給源(PlacementTargetCatalog.UnlockedEntries)から表示名で割当てる。未解放対象は割当不可
        // Assigns by display name from the same source as the build menu (PlacementTargetCatalog.UnlockedEntries); locked targets cannot be assigned
        public async UniTask AssignHotbar(int slot, string targetName) => await _reporter.Act($"ホットバー{slot + 1}へ割当: {targetName}", () => PlaytestHotbarOps.AssignHotbar(slot, targetName, 15f));

        public void UnlockConnectTool(string toolName)
        {
            // 接続ツール(電線/レール/歯車チェーン等)はブロックとは別枠のアンロック状態を持つため、AssignHotbar前に必要
            // Connect tools (wire/rail/gear chain, etc.) hold a separate unlock state from blocks, so this precedes AssignHotbar
            _reporter.Step($"接続ツールをアンロック: {toolName}");
            PlaytestHotbarOps.UnlockConnectToolServerSide(toolName);
        }

        public void UnlockBlueprint()
        {
            // ブループリントはブロックとは別枠のアンロック状態を持つため、AssignHotbar前に必要
            // Blueprint holds a separate unlock state from blocks, so this precedes AssignHotbar
            _reporter.Step("ブループリントをアンロック");
            PlaytestHotbarOps.UnlockBlueprintServerSide();
        }
    }
}
