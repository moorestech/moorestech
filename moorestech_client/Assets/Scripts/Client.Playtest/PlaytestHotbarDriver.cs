using Client.Playtest.Core;
using Client.Playtest.Input;
using Client.Playtest.Operations;
using Cysharp.Threading.Tasks;
using UnityEngine.InputSystem;

namespace Client.Playtest
{
    /// <summary>
    ///     ホットバー操作のサブファサード。PlaytestDriver.Hotbar経由でシナリオから呼ぶ
    ///     Hotbar operation sub-facade, called from scenarios via PlaytestDriver.Hotbar
    /// </summary>
    public class PlaytestHotbarDriver
    {
        private readonly PlaytestReporter _reporter;

        public PlaytestHotbarDriver(PlaytestReporter reporter)
        {
            _reporter = reporter;
        }

        /// <summary>
        ///     slotは0始まり(0→キー1)。持ち替えではなく建築モードのトグル: 割当済み対象を持って入り、同じ枠で抜ける
        ///     slot is zero-based (0 -> key "1"); a build-mode toggle, not an item swap: enters holding the assigned target, exits on the same slot
        /// </summary>
        public async UniTask SelectHotbar(int slot) => await _reporter.Act($"ホットバー{slot + 1}をタップ", () => SemanticInput.TapKey(Key.Digit1 + slot));

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
    }
}
