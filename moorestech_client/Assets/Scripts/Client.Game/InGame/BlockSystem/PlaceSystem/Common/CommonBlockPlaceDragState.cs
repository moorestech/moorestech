using Client.Input;
using Core.Master;
using UnityEngine;

namespace Client.Game.InGame.BlockSystem.PlaceSystem.Common
{
    /// <summary>
    /// ドラッグ状態と高さオフセットを保持
    /// Holds the drag state and height offset
    /// 終了時に高さは開始値へ戻す
    /// Ending a drag restores the starting height
    /// </summary>
    public class CommonBlockPlaceDragState
    {
        public int HeightOffset { get; private set; }

        private Vector3Int? _clickStartPosition;
        private int _clickStartHeightOffset;
        private BlockId? _previousSelectedBlockId;

        public void SetClickStartHeightOffset(int clickStartHeightOffset)
        {
            _clickStartHeightOffset = clickStartHeightOffset;
        }

        public void ClearDrag()
        {
            _clickStartPosition = null;
        }

        public void UpdateHeightOffsetByInput()
        {
            if (HybridInput.GetKeyDown(KeyCode.Q)) //TODO InputManagerに移す
                HeightOffset--;
            else if (HybridInput.GetKeyDown(KeyCode.E)) HeightOffset++;
        }

        // 選択ブロック変更時に連続設置状態と高さ基準をリセット
        // Resets drag state and the height anchor when the selected block changes
        public void SyncSelectedBlock(BlockId blockId)
        {
            if (_previousSelectedBlockId != blockId)
            {
                _clickStartPosition = null;
                _clickStartHeightOffset = HeightOffset;
            }
            _previousSelectedBlockId = blockId;
        }

        public void BeginDrag(Vector3Int clickStartPosition)
        {
            _clickStartPosition = clickStartPosition;
            _clickStartHeightOffset = HeightOffset;
        }

        public Vector3Int ResolveDragStartPoint(Vector3Int placePoint)
        {
            return _clickStartPosition ?? placePoint;
        }

        // マウスアップで連続設置解除、高さを開始時へ戻す。戻り値は押下が登録されていたか
        // Clears the drag state on mouse-up and restores the starting height; returns whether a press was registered
        public bool EndDrag()
        {
            // 押下未登録の解放は無視する（ビルドメニュー選択クリックの解放が漏れ、Enableのセンチネル-1を高さへ書き込むのを防ぐ）
            // Ignore releases without a registered press (a leaked build-menu click release would write Enable's -1 sentinel into the height)
            if (!_clickStartPosition.HasValue) return false;

            HeightOffset = _clickStartHeightOffset;
            _clickStartPosition = null;
            return true;
        }
    }
}
