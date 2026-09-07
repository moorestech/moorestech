using System;
using Client.Game.InGame.UI.UIState;
using Client.Playtest.Input;
using Client.Playtest.WebUi;
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
        public static async UniTask OpenBuildMenuAndSelectBlock(string blockName)
        {
            // 画面UIはWeb UI一本のため、CEFが無い環境では操作経路が存在しない。ブラウザ生成は非同期のため期限付きで待つ
            // The screen UI is Web-only, so without CEF there is no operation path; browser creation is async so poll with a deadline
            var webUiDeadline = Time.realtimeSinceStartup + 15f;
            while (!CefScreenMapper.IsWebUiAvailable())
            {
                if (webUiDeadline <= Time.realtimeSinceStartup) throw new InvalidOperationException("Build menu operations require the Web UI (CEF) to be available");
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
            var webUiTestid = PlaytestWebUiOps.BuildMenuBlockTestId(blockName);
            await PlaytestWebUiOps.WaitWebUiElement("build-menu-panel", 15f);

            // 全カテゴリが1本のスクロールに並ぶため、対象ブロックのカテゴリ見出しへ送って視界に入れる
            // Every category shares one scroll list, so click the target block's category to bring its section into view
            var blockMaster = MasterHolder.BlockMaster.GetBlockMaster(blockId);
            var (categoryGuid, _) = MasterHolder.BuildMenuCategoryMaster
                .GetGuidPair(blockMaster.Category, blockMaster.SubCategory);
            await PlaytestWebUiOps.ClickWebUi($"build-menu-category-{categoryGuid:D}", 15f);

            // 非同期BPライブラリ更新が選択を破棄するレースに備え、PlaceBlock遷移までクリックを繰り返す
            // Retry clicks until PlaceBlock to survive an async blueprint-library rebuild discarding selection
            var deadline = Time.realtimeSinceStartup + 15f;
            while (PlaytestUiOps.CurrentUiState() != UIStateEnum.PlaceBlock)
            {
                var remainingSeconds = deadline - Time.realtimeSinceStartup;
                if (remainingSeconds <= 0f) throw new TimeoutException($"Build menu selection did not reach PlaceBlock: {blockName}");
                await PlaytestWebUiOps.ClickWebUi(webUiTestid, remainingSeconds);
                await UniTask.DelayFrame(10);
            }

            // カメラtween収束を待機
            // Wait for the camera tween to settle
            await UniTask.Delay(TimeSpan.FromSeconds(0.6f));
        }
    }
}
