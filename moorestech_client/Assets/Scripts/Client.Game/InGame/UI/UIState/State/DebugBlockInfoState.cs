using Client.Game.InGame.Block;
using Client.Game.InGame.Control;
using Client.Game.InGame.UI.KeyControl;
using Client.Game.InGame.UI.UIState.Input;
using Client.Input;
using UnityEngine;

namespace Client.Game.InGame.UI.UIState.State
{
    public class DebugBlockInfoState : IUIState
    {
        private readonly ScreenClickableCameraController _screenClickableCameraController;
        private BlockGameObject _hoveredBlock;

        public DebugBlockInfoState(InGameCameraController inGameCameraController)
        {
            _screenClickableCameraController = new ScreenClickableCameraController(inGameCameraController);
        }

        public void OnEnter(UITransitContext context)
        {
            // マウス自由操作モードに入り、右ドラッグでカメラ操作
            // Enter free-cursor mode; right-drag controls the camera
            _screenClickableCameraController.OnEnter(false);
            KeyControlDescription.Instance.SetText("左クリック: ブロック情報をログ出力\nESC / F3: デバッグモード終了");
        }

        public UITransitContext GetNextUpdate()
        {
            // ESCまたはF3でGameScreenへ戻る
            // Return to GameScreen on ESC or F3
            if (InputManager.UI.CloseUI.GetKeyDown) return new UITransitContext(UIStateEnum.GameScreen);
            //TODO InputSystemのリファクタ対象
            if (UnityEngine.Input.GetKeyDown(KeyCode.F3)) return new UITransitContext(UIStateEnum.GameScreen);

            // カーソル下のブロックにバウンディングボックスを表示
            // Show bounding box on the block under the cursor
            UpdateHoverPreview();

            // 左クリックでカーソル下のブロック情報をログに出力
            // Log the block info at the cursor on left click
            if (InputManager.Playable.ScreenLeftClick.GetKeyDown)
            {
                LogClickedBlockInfo();
            }

            _screenClickableCameraController.GetNextUpdate();
            return null;

            #region Internal

            void UpdateHoverPreview()
            {
                BlockClickDetectUtil.TryGetCursorOnBlock(out var hovered);

                if (_hoveredBlock == hovered) return;

                if (_hoveredBlock != null) _hoveredBlock.EnablePreviewOnlyObjects(false, false);
                if (hovered != null) hovered.EnablePreviewOnlyObjects(true, true);

                _hoveredBlock = hovered;
            }

            void LogClickedBlockInfo()
            {
                if (!BlockClickDetectUtil.TryGetCursorOnBlock(out var blockObject))
                {
                    Debug.Log("[DebugBlockInfo] カーソル下にブロックが見つかりませんでした。");
                    return;
                }

                var master = blockObject.BlockMasterElement;
                var pos = blockObject.BlockPosInfo.OriginalPos;
                Debug.Log(
                    $"[DebugBlockInfo] Name={master.Name} " +
                    $"BlockId={blockObject.BlockId} " +
                    $"InstanceId={blockObject.BlockInstanceId} " +
                    $"Guid={master.BlockGuid} " +
                    $"Pos={pos} " +
                    $"Direction={blockObject.BlockPosInfo.BlockDirection}");
            }

            #endregion
        }

        public void OnExit()
        {
            // ホバー中のバウンディングボックスを消してから終了
            // Hide the hovered bounding box before exiting
            ClearHoverPreview();
            _screenClickableCameraController.OnExit();
        }

        private void ClearHoverPreview()
        {
            if (_hoveredBlock == null) return;
            _hoveredBlock.EnablePreviewOnlyObjects(false, false);
            _hoveredBlock = null;
        }
    }
}
