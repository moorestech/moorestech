using Client.Game.InGame.Block;
using Client.Game.InGame.Control;
using Client.Game.InGame.UI.KeyControl;
using Client.Input;
using UnityEngine;

namespace Client.Game.InGame.UI.UIState.State
{
    public class DebugBlockInfoState : IUIState
    {
        private readonly InGameCameraController _inGameCameraController;
        private BlockGameObject _hoveredBlock;

        public DebugBlockInfoState(InGameCameraController inGameCameraController)
        {
            _inGameCameraController = inGameCameraController;
        }

        public void OnEnter(UITransitContext context)
        {
            // マウス自由操作モードに入り、右ドラッグでカメラ操作
            // Enter free-cursor mode; right-drag controls the camera
            InputManager.MouseCursorVisible(true);
            KeyControlDescription.Instance.SetText("左クリック: ブロック情報をログ出力\nESC / F3: デバッグモード終了");
        }

        public UITransitContext GetNextUpdate()
        {
            // ESCまたはF3でGameScreenへ戻る
            // Return to GameScreen on ESC or F3
            if (InputManager.UI.CloseUI.GetKeyDown) return new UITransitContext(UIStateEnum.GameScreen);
            //TODO InputSystem対応
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

            //TODO InputSystem対応
            if (UnityEngine.Input.GetMouseButtonDown(1))
            {
                InputManager.MouseCursorVisible(false);
                _inGameCameraController.SetControllable(true);
            }
            //TODO InputSystem対応
            if (UnityEngine.Input.GetMouseButtonUp(1))
            {
                InputManager.MouseCursorVisible(true);
                _inGameCameraController.SetControllable(false);
            }
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
                // スタックトレースを一時的に無効化してログ出力
                // Temporarily disable stack trace for this log
                var previous = Application.GetStackTraceLogType(LogType.Log);
                Application.SetStackTraceLogType(LogType.Log, StackTraceLogType.None);
                try
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
                finally
                {
                    Application.SetStackTraceLogType(LogType.Log, previous);
                }
            }

            #endregion
        }

        public void OnExit()
        {
            // ホバー中のバウンディングボックスを消してから終了
            // Hide the hovered bounding box before exiting
            if (_hoveredBlock != null)
            {
                _hoveredBlock.EnablePreviewOnlyObjects(false, false);
                _hoveredBlock = null;
            }
            InputManager.MouseCursorVisible(false);
        }
    }
}
