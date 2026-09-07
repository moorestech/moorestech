using System;
using System.Linq;
using Client.Game.InGame.Context;
using Client.Game.InGame.UI.BuildMenu;
using Client.Game.InGame.UI.Inventory.Common;
using Client.Game.InGame.UI.UIState;
using Client.Playtest.Input;
using Client.Playtest.WebUi;
using Core.Master;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.EventSystems;
using Object = UnityEngine.Object;

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
            // CEF有無で操作経路を切替
            // Switch the operation path depending on whether CEF is present
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

            // CEFではパネル表示を待ち、BlockGuid由来の安定testidで対象エントリを選択する
            // Under CEF, wait for the panel and select the entry by its stable BlockGuid-derived testid
            var useWebUi = CefScreenMapper.IsWebUiAvailable();
            var blockId = PlaytestBlockOps.ResolveBlockId(blockName);
            var webUiTestid = PlaytestWebUiOps.BuildMenuBlockTestId(blockName);
            if (useWebUi)
            {
                await PlaytestWebUiOps.WaitWebUiElement("build-menu-panel", 15f);

                // 全カテゴリが1本のスクロールに並ぶため、対象ブロックのカテゴリ見出しへ送って視界に入れる
                // Every category shares one scroll list, so click the target block's category to bring its section into view
                var blockMaster = MasterHolder.BlockMaster.GetBlockMaster(blockId);
                var (categoryGuid, _) = MasterHolder.BuildMenuCategoryMaster
                    .GetGuidPair(blockMaster.Category, blockMaster.SubCategory);
                await PlaytestWebUiOps.ClickWebUi($"build-menu-category-{categoryGuid:D}", 15f);
            }

            // 非同期BPライブラリ更新が選択を破棄するレースに備え、PlaceBlock遷移までクリックを繰り返す
            // Retry clicks until PlaceBlock to survive an async blueprint-library rebuild discarding selection
            var deadline = Time.realtimeSinceStartup + 15f;
            while (PlaytestUiOps.CurrentUiState() != UIStateEnum.PlaceBlock)
            {
                var remainingSeconds = deadline - Time.realtimeSinceStartup;
                if (remainingSeconds <= 0f) throw new TimeoutException($"Build menu selection did not reach PlaceBlock: {blockName}");
                if (useWebUi)
                {
                    await PlaytestWebUiOps.ClickWebUi(webUiTestid, remainingSeconds);
                }
                else
                {
                    TryClickBuildMenuSlot(blockName);
                }
                await UniTask.DelayFrame(10);
            }

            // カメラtween収束を待機
            // Wait for the camera tween to settle
            await UniTask.Delay(TimeSpan.FromSeconds(0.6f));
        }

        private static bool TryClickBuildMenuSlot(string blockName)
        {
            // 対象ブロックのアイコンViewData（BlockIdごとにキャッシュされた同一インスタンス）でスロットを特定する
            // Locate the slot by the block's cached icon ItemViewData instance (one per BlockId)
            var blockId = PlaytestBlockOps.ResolveBlockId(blockName);
            var iconView = ClientContext.BlockImageContainer.GetBlockView(blockId);

            // 再構築中はスロットが一時的に存在しないため、見つからなければ失敗を返しリトライに任せる
            // Slots vanish transiently during a rebuild, so return false and let the caller retry
            var buildMenuView = Object.FindFirstObjectByType<BuildMenuView>(FindObjectsInactive.Include);
            var slot = buildMenuView.GetComponentsInChildren<ItemSlotView>(true)
                .FirstOrDefault(s => s.ItemViewData != null && ReferenceEquals(s.ItemViewData, iconView));
            if (slot == null) return false;

            var clickTarget = slot.GetComponentInChildren<CommonSlotView>(true).gameObject;
            var eventData = new PointerEventData(EventSystem.current) { button = PointerEventData.InputButton.Left };
            ExecuteEvents.Execute(clickTarget, eventData, ExecuteEvents.pointerDownHandler);
            ExecuteEvents.Execute(clickTarget, eventData, ExecuteEvents.pointerUpHandler);
            return true;
        }
    }
}
