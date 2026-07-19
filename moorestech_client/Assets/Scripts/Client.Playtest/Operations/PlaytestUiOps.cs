using System;
using System.Linq;
using ClassLibrary;
using Client.Game.InGame.Context;
using Client.Game.InGame.UI.BuildMenu;
using Client.Game.InGame.UI.Inventory.Common;
using Client.Game.InGame.UI.UIState;
using Client.Playtest.Input;
using Client.Playtest.WebUi;
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
        private const float AimGlideSeconds = 0.3f;
        private const float DragGlideSeconds = 0.5f;

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
                if (timeoutSeconds < Time.realtimeSinceStartup - startTime) throw new TimeoutException($"UI state did not reach {expected} (current: {CurrentUiState()})");
                await UniTask.Yield();
            }
        }

        public static async UniTask OpenBuildMenuAndSelectBlock(string blockName)
        {
            // ビルドメニューを開き、CEF利用時はWeb UI、未利用時はEventSystem経由で対象をクリックする
            // Open the build menu and click via Web UI under CEF, or EventSystem when CEF is absent
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

            // CEFではパネル表示を待ち、BlockId由来の安定testidで対象エントリを選択する
            // Under CEF, wait for the panel and select the entry by its stable BlockId-derived testid
            var useWebUi = CefScreenMapper.IsWebUiAvailable();
            var blockId = PlaytestBlockOps.ResolveBlockId(blockName);
            var webUiTestid = $"build-menu-entry-block-{blockId.AsPrimitive()}";
            if (useWebUi) await PlaytestWebUiOps.WaitWebUiElement("build-menu-panel", 15f);

            // 非同期BPライブラリ更新が選択を破棄するレースに備え、PlaceBlock遷移までクリックを繰り返す
            // Retry clicks until PlaceBlock to survive an async blueprint-library rebuild discarding selection
            var deadline = Time.realtimeSinceStartup + 15f;
            while (CurrentUiState() != UIStateEnum.PlaceBlock)
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
            // PlaceBlock遷移直後のカメラtween中に照準するとレイが空を向き設置レイキャストが外れるため、静定を待つ
            // Aiming mid camera-tween (right after entering PlaceBlock) points the ray at the sky, so wait until it settles
            await WaitCameraSettled();

            // ワールド座標をスクリーン座標へ変換しマウス絶対座標を注入、プレビュー更新を1フレーム以上待つ
            // Convert world position to screen space, inject the absolute mouse position, wait for the preview to update
            var screenPosition = Camera.main.WorldToScreenPoint(worldPosition);

            // 画面外への滑走は「UI上状態が解除されない・クリックが空振る」を黙って起こすため即座に失敗させる
            // Gliding off-screen silently causes stuck pointer-over state and missed clicks, so fail fast instead
            if (screenPosition.z <= 0f || screenPosition.x < 0f || Screen.width < screenPosition.x || screenPosition.y < 0f || Screen.height < screenPosition.y)
            {
                throw new InvalidOperationException($"Aim point {worldPosition} projects off-screen ({screenPosition}). Warp the player so the target is in front of the placement camera (camera faces north with a shallow pitch).");
            }
            await SemanticInput.MouseGlideTo(screenPosition, AimGlideSeconds);
            await UniTask.DelayFrame(3);
        }

        private static async UniTask WaitCameraSettled()
        {
            // 位置と回転が3フレーム連続で不変になるまで待つ（固定sleep禁止の代替。上限3秒で諦めて続行）
            // Wait until position and rotation stay unchanged for 3 consecutive frames (no fixed sleeps; give up after 3s)
            var cameraTransform = Camera.main.transform;
            var deadline = Time.realtimeSinceStartup + 3f;
            var stableFrames = 0;
            var lastPosition = cameraTransform.position;
            var lastRotation = cameraTransform.rotation;
            while (stableFrames < 3 && Time.realtimeSinceStartup < deadline)
            {
                await UniTask.Yield();
                var moved = 0.0005f < (cameraTransform.position - lastPosition).sqrMagnitude || 0.05f < Quaternion.Angle(cameraTransform.rotation, lastRotation);
                stableFrames = moved ? 0 : stableFrames + 1;
                lastPosition = cameraTransform.position;
                lastRotation = cameraTransform.rotation;
            }
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
            await WaitPointerLeaveWebUi();
            await SemanticInput.Click();
        }

        public static async UniTask DragPlace(Vector3 fromWorldPosition, Vector3 toWorldPosition)
        {
            // 始点で押下→終点へ移動→解放。設置はボタン解放（GetKeyUp）で確定する
            // Press at the start, move to the end, release; placement commits on button release (GetKeyUp)
            await AimAtWorldPosition(fromWorldPosition);
            await WaitPointerLeaveWebUi();
            SemanticInput.MouseButtonDown(0);
            await UniTask.DelayFrame(3);
            var endScreenPosition = Camera.main.WorldToScreenPoint(toWorldPosition);
            await SemanticInput.MouseGlideTo(endScreenPosition, DragGlideSeconds);
            await UniTask.DelayFrame(3);
            SemanticInput.MouseButtonUp(0);
            await UniTask.DelayFrame(3);
        }


        private static async UniTask WaitPointerLeaveWebUi()
        {
            // Web UIの被覆判定はページからのWS通知で非同期更新されるため、直前のUIクリックの「UI上」状態が
            // 解除されるのを待ってから設置クリックする（待たないと押下がIsPointerOverAnyUiに弾かれるレースになる）
            // The Web UI pointer-over state updates asynchronously via WS notifications from the page, so wait for the
            // previous UI click's over-UI state to clear before place clicks (otherwise IsPointerOverAnyUi eats the press)
            var deadline = Time.realtimeSinceStartup + 2f;
            while (Client.Input.WebUiInputExclusivity.IsPointerOverWebUi)
            {
                if (deadline < Time.realtimeSinceStartup)
                {
                    Debug.LogWarning("[Playtest] pointer still over Web UI before a place click; proceeding anyway");
                    return;
                }
                await UniTask.Yield();
            }
        }

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

    }
}
