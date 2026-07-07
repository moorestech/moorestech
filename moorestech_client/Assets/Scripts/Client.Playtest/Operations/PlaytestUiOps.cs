using System;
using System.Linq;
using ClassLibrary;
using Client.Game.InGame.Context;
using Client.Game.InGame.UI.BuildMenu;
using Client.Game.InGame.UI.Inventory.Common;
using Client.Game.InGame.UI.UIState;
using Client.Playtest.Input;
using Core.Master;
using Cysharp.Threading.Tasks;
using Game.Block.Interface;
using UnityEngine;
using UnityEngine.EventSystems;
using Object = UnityEngine.Object;

namespace Client.Playtest.Operations
{
    /// <summary>
    ///     UI経路（ビルドメニュー・設置プレビュー・クリック設置）の操作群
    ///     Operations for the UI route: build menu, placement preview, and click-to-place
    /// </summary>
    public static class PlaytestUiOps
    {
        public static UIStateEnum CurrentUiState()
        {
            return Object.FindFirstObjectByType<UIStateControl>().CurrentState;
        }

        public static async UniTask WaitUiState(UIStateEnum expected, float timeoutSeconds)
        {
            // UIStateControlのUpdateが遷移を消化するまでフレームポーリングで待つ
            // Poll per frame until UIStateControl's Update consumes the transition
            var startTime = Time.realtimeSinceStartup;
            while (CurrentUiState() != expected)
            {
                if (Time.realtimeSinceStartup - startTime > timeoutSeconds) throw new TimeoutException($"UI state did not reach {expected} (current: {CurrentUiState()})");
                await UniTask.Yield();
            }
        }

        public static async UniTask OpenBuildMenuAndSelectBlock(string blockName)
        {
            // ビルドメニューを開き、対象ブロックのスロットをEventSystem経由でクリックする
            // Open the build menu, then click the target block's slot through the EventSystem
            // PlaceBlock中はBだとGameScreenへ抜けてしまうためTabで開き直す（実プレイと同じキー割当）
            // While in PlaceBlock, B exits to GameScreen, so reopen with Tab (same binding as real play)
            if (CurrentUiState() == UIStateEnum.PlaceBlock)
            {
                await SemanticInput.TapKey(UnityEngine.InputSystem.Key.Tab);
                await WaitUiState(UIStateEnum.BuildMenu, 10f);
            }
            else if (CurrentUiState() != UIStateEnum.BuildMenu)
            {
                await SemanticInput.TapKey(UnityEngine.InputSystem.Key.B);
                await WaitUiState(UIStateEnum.BuildMenu, 10f);
            }

            ClickBuildMenuSlot(blockName);
            await WaitUiState(UIStateEnum.PlaceBlock, 10f);

            // PlaceBlock遷移直後のカメラtween（トップダウン化）が落ち着くまで待つ
            // Wait for the camera tween (to top-down) right after entering PlaceBlock to settle
            await UniTask.Delay(TimeSpan.FromSeconds(0.6f));
        }

        public static async UniTask ExitToGameScreen()
        {
            if (CurrentUiState() == UIStateEnum.GameScreen) return;
            await SemanticInput.TapKey(UnityEngine.InputSystem.Key.B);
            await WaitUiState(UIStateEnum.GameScreen, 10f);
        }

        public static async UniTask AimAtWorldPosition(Vector3 worldPosition)
        {
            // ワールド座標をスクリーン座標へ変換しマウス絶対座標を注入、プレビュー更新を1フレーム以上待つ
            // Convert world position to screen space, inject the absolute mouse position, wait for the preview to update
            var screenPosition = Camera.main.WorldToScreenPoint(worldPosition);
            SemanticInput.MouseMoveTo(screenPosition);
            await UniTask.DelayFrame(3);
        }

        public static Vector3 PlaceAimPoint(string blockName, Vector3Int origin, BlockDirection direction)
        {
            // CalcPlacePointの逆算: 接地面上のフットプリント中心を狙えば指定originに設置される
            // Inverse of CalcPlacePoint: aiming at the footprint center on the ground surface yields the given origin
            var blockId = PlaytestBlockOps.ResolveBlockId(blockName);
            var blockMaster = MasterHolder.BlockMaster.GetBlockMaster(blockId);
            var rotatedSize = direction.GetCoordinateConvertAction()(blockMaster.BlockSize).Abs();
            return new Vector3(origin.x + rotatedSize.x / 2f, origin.y, origin.z + rotatedSize.z / 2f);
        }

        public static async UniTask ClickPlace()
        {
            await SemanticInput.Click();
        }

        public static async UniTask DragPlace(Vector3 fromWorldPosition, Vector3 toWorldPosition)
        {
            // 始点で押下→終点へ移動→解放。設置はボタン解放（GetKeyUp）で確定する
            // Press at the start, move to the end, release; placement commits on button release (GetKeyUp)
            await AimAtWorldPosition(fromWorldPosition);
            SemanticInput.MouseButtonDown(0);
            await UniTask.DelayFrame(3);
            await AimAtWorldPosition(toWorldPosition);
            SemanticInput.MouseButtonUp(0);
            await UniTask.DelayFrame(3);
        }

        #region Internal

        private static void ClickBuildMenuSlot(string blockName)
        {
            // 対象ブロックのアイコンViewData（BlockIdごとにキャッシュされた同一インスタンス）でスロットを特定する
            // Locate the slot by the block's cached icon ItemViewData instance (one per BlockId)
            var blockId = PlaytestBlockOps.ResolveBlockId(blockName);
            var iconView = ClientContext.BlockImageContainer.GetBlockView(blockId);

            var buildMenuView = Object.FindFirstObjectByType<BuildMenuView>(FindObjectsInactive.Include);
            var slot = buildMenuView.GetComponentsInChildren<ItemSlotView>(true)
                .FirstOrDefault(s => s.ItemViewData != null && ReferenceEquals(s.ItemViewData, iconView));
            if (slot == null) throw new InvalidOperationException($"Build menu slot not found for block: {blockName} (unlocked?)");

            var clickTarget = slot.GetComponentInChildren<CommonSlotView>(true).gameObject;
            var eventData = new PointerEventData(EventSystem.current) { button = PointerEventData.InputButton.Left };
            ExecuteEvents.Execute(clickTarget, eventData, ExecuteEvents.pointerDownHandler);
            ExecuteEvents.Execute(clickTarget, eventData, ExecuteEvents.pointerUpHandler);
        }

        #endregion
    }
}
