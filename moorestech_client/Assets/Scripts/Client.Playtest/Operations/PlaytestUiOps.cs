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
            // キー1回のタップ取りこぼしに備え、開くまでタップを繰り返す
            // Retry the open key in case a single tap is dropped
            var openKey = CurrentUiState() == UIStateEnum.PlaceBlock ? UnityEngine.InputSystem.Key.Tab : UnityEngine.InputSystem.Key.B;
            for (var attempt = 0; attempt < 3 && CurrentUiState() != UIStateEnum.BuildMenu; attempt++)
            {
                await SemanticInput.TapKey(openKey);
                if (await PollUiState(UIStateEnum.BuildMenu, 4f)) break;
            }
            if (CurrentUiState() != UIStateEnum.BuildMenu) throw new TimeoutException($"Build menu did not open (current: {CurrentUiState()})");

            // 非同期BPライブラリ更新の再構築がクリック済み選択を破棄するレースがあるため、遷移するまでクリックを繰り返す
            // The async blueprint-library rebuild can wipe a clicked selection, so retry clicking until the transition happens
            var deadline = Time.realtimeSinceStartup + 15f;
            while (CurrentUiState() != UIStateEnum.PlaceBlock)
            {
                if (Time.realtimeSinceStartup > deadline) throw new TimeoutException($"Build menu selection did not reach PlaceBlock: {blockName}");
                TryClickBuildMenuSlot(blockName);
                await UniTask.DelayFrame(10);
            }

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

        private static async UniTask<bool> PollUiState(UIStateEnum expected, float seconds)
        {
            // 例外を投げないUIState待ち（リトライループ用）
            // Non-throwing UI-state wait for retry loops
            var deadline = Time.realtimeSinceStartup + seconds;
            while (Time.realtimeSinceStartup < deadline)
            {
                if (CurrentUiState() == expected) return true;
                await UniTask.DelayFrame(5);
            }
            return false;
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

        #endregion
    }
}
