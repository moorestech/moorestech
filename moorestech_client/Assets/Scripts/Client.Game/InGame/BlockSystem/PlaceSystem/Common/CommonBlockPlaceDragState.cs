using Client.Input;
using Core.Master;
using UnityEngine;

namespace Client.Game.InGame.BlockSystem.PlaceSystem.Common
{
    /// <summary>
    /// 通常設置の連続設置（ドラッグ）状態と設置高さオフセットを保持する。ドラッグ終了で高さは開始時の値へ戻る
    /// Holds normal placement's continuous-placement (drag) state and the height offset; ending a drag restores the height it started at
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

        // ビルドメニューの選択ブロックが変わったら連続設置状態をリセットし、高さの戻り先を現在値にする
        // Reset the continuous placement state when the build-menu selected block changes, and re-anchor the height offset to the current one
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

        // マウスを離したので連続設置状態は解除し、高さをドラッグ開始時へ戻す
        // Clear the continuous-placement state on mouse release and restore the height offset from the drag start
        public void EndDrag()
        {
            HeightOffset = _clickStartHeightOffset;
            _clickStartPosition = null;
        }
    }
}
