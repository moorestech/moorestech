using System;
using Client.Game.InGame.UI.UIState;
using Client.Playtest.Input;
using Client.Playtest.UiElements;
using Core.Master;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Client.Playtest.Operations.Ui
{
    /// <summary>
    ///     ビルドメニューを開いて設置ブロックを選ぶまでのUI経路操作
    ///     UI-route operations for opening the build menu and selecting a block to place
    /// </summary>
    public static class PlaytestBuildMenuOps
    {
        // PlaytestBuildMenuOps・Driver・シナリオでBlockGuid由来testid生成を共有し、契約の二重定義を防ぐ
        // Share BlockGuid-derived testid generation across build-menu ops, the driver, and scenarios to prevent drift
        public static string BuildMenuBlockTestId(string blockName)
        {
            var blockGuid = MasterHolder.BlockMaster.GetBlockMaster(PlaytestBlockOps.ResolveBlockId(blockName)).BlockGuid;
            return $"build-menu-entry-block-{blockGuid}";
        }

        public static async UniTask OpenBuildMenuAndSelectBlock(string blockName)
        {
            // 画面UIの描画系が使えるようになるまで期限付きで待つ（CEFのブラウザ生成は非同期）
            // Wait with a deadline for the screen-UI renderer to become available (CEF browser creation is async)
            var rendererDeadline = Time.realtimeSinceStartup + 15f;
            while (!PlaytestUiLocatorSession.Locator.IsAvailable())
            {
                if (rendererDeadline <= Time.realtimeSinceStartup) throw new InvalidOperationException("Build menu operations require an available UI renderer");
                await UniTask.DelayFrame(5);
            }

            // PlaceBlock中はBだとGameScreenへ抜けてしまうためTabで開き直す（実プレイと同じキー割当）
            // While in PlaceBlock, B exits to GameScreen, so reopen with Tab (same binding as real play)
            // キー1回のタップ取りこぼしに備え、開くまでタップを繰り返す
            // Retry the open key in case a single tap is dropped
            var openKey = PlaytestUiOps.CurrentUiState() == UIStateEnum.PlaceBlock ? UnityEngine.InputSystem.Key.Tab : UnityEngine.InputSystem.Key.B;
            for (var attempt = 0; attempt < 3 && PlaytestUiOps.CurrentUiState() != UIStateEnum.BuildMenu; attempt++)
            {
                await SemanticInput.TapKey(openKey);
                if (await PlaytestUiOps.PollUiState(UIStateEnum.BuildMenu, 4f)) break;
            }
            if (PlaytestUiOps.CurrentUiState() != UIStateEnum.BuildMenu) throw new TimeoutException($"Build menu did not open (current: {PlaytestUiOps.CurrentUiState()})");

            // パネル表示を待ち、BlockGuid由来の安定testidで対象エントリを選択する
            // Wait for the panel and select the entry by its stable BlockGuid-derived testid
            var blockId = PlaytestBlockOps.ResolveBlockId(blockName);
            var entryTestid = BuildMenuBlockTestId(blockName);
            await PlaytestUiElementOps.WaitUiElement("build-menu-panel", 15f);

            // 全カテゴリが1本のスクロールに並ぶため、対象ブロックのカテゴリ見出しへ送って視界に入れる
            // Every category shares one scroll list, so click the target block's category to bring its section into view
            var blockMaster = MasterHolder.BlockMaster.GetBlockMaster(blockId);
            var (categoryGuid, _) = MasterHolder.BuildMenuCategoryMaster
                .GetGuidPair(blockMaster.Category, blockMaster.SubCategory);
            await PlaytestUiElementOps.ClickUi($"build-menu-category-{categoryGuid:D}", 15f);
            await PlaytestUiElementOps.ScrollDownUntilUiElementIsClickable(entryTestid, "build-menu-scroll-area", 15f);

            // 非同期BPライブラリ更新が選択を破棄するレースに備え、PlaceBlock遷移までクリックを繰り返す
            // Retry clicks until PlaceBlock to survive an async blueprint-library rebuild discarding selection
            var deadline = Time.realtimeSinceStartup + 15f;
            while (PlaytestUiOps.CurrentUiState() != UIStateEnum.PlaceBlock)
            {
                var remainingSeconds = deadline - Time.realtimeSinceStartup;
                if (remainingSeconds <= 0f) throw new TimeoutException($"Build menu selection did not reach PlaceBlock: {blockName}");
                await PlaytestUiElementOps.ClickUi(entryTestid, remainingSeconds);
                await UniTask.DelayFrame(10);
            }

            // 選択後の状態遷移が収まるまで待機
            // Wait for the post-selection state transition to settle
            await UniTask.Delay(TimeSpan.FromSeconds(0.6f));
        }
    }
}
