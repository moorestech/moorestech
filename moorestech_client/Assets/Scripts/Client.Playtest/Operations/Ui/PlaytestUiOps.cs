using System;
using ClassLibrary;
using Client.Game.InGame.UI.UIState;
using Client.Playtest.Input;
using Core.Master;
using Cysharp.Threading.Tasks;
using Game.Block.Interface;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Client.Playtest.Operations.Ui
{
    /// <summary>
    ///     UI状態の観測と、設置プレビューの照準・クリック設置の操作群
    ///     Operations for observing UI state and for aiming / click-to-place during the placement preview
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

        public static async UniTask<bool> PollUiState(UIStateEnum expected, float seconds)
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
    }
}
