using System.Collections.Generic;
using Game.Block.Interface;
using Mooresmaster.Model.BlocksModule;
using Server.Protocol.PacketResponse;
using UnityEngine;

namespace Client.Game.InGame.BlockSystem.PlaceSystem.BeltConveyor.Parts
{
    /// <summary>
    /// ドラッグ設置の連続状態を追跡しセル列を算出する
    /// Tracks the drag placement state and computes the cell sequence
    /// </summary>
    internal class BeltConveyorClickDragTracker
    {
        private Vector3Int? _clickStartPosition;
        private int _clickStartHeightOffset;
        private bool? _isStartZDirection;

        public bool HasClickStart => _clickStartPosition.HasValue;

        public void SetInitialHeightOffset(int heightOffset) => _clickStartHeightOffset = heightOffset;

        // 選択ブロック変更時に連続設置状態をリセットする
        // Reset the continuous-placement state when the selected block changes
        public void ResetForSelectionChange(int heightOffset)
        {
            _clickStartPosition = null;
            _clickStartHeightOffset = heightOffset;
        }

        // Disable()時に連続設置状態のみを解除する
        // Clear only the continuous-placement state on Disable()
        public void ResetDragState()
        {
            _clickStartPosition = null;
            _isStartZDirection = null;
        }

        public void RegisterClickStart(Vector3Int placePoint, int heightOffset)
        {
            _clickStartPosition = placePoint;
            _clickStartHeightOffset = heightOffset;
        }

        // クリック開始位置から現在位置までのセル列を算出する
        // Compute the cell sequence from the click-start position to the current position
        public List<PlaceInfo> ComputeCellInfos(Vector3Int placePoint, BlockDirection currentBlockDirection, BlockMasterElement holdingBlockMaster, BeltConveyorPlacePointCalculator calculator)
        {
            if (_clickStartPosition.HasValue)
            {
                if (_clickStartPosition.Value == placePoint)
                {
                    _isStartZDirection = null;
                }
                else if (!_isStartZDirection.HasValue)
                {
                    _isStartZDirection = Mathf.Abs(placePoint.x - _clickStartPosition.Value.x) < Mathf.Abs(placePoint.z - _clickStartPosition.Value.z);
                }

                return calculator.CalculatePoint(_clickStartPosition.Value, placePoint, _isStartZDirection ?? true, currentBlockDirection, holdingBlockMaster);
            }

            _isStartZDirection = null;
            return calculator.CalculatePoint(placePoint, placePoint, true, currentBlockDirection, holdingBlockMaster);
        }

        // マウス解放で連続設置状態を解除し高さ差分を返す
        // Clear the state on mouse release and return the height offset
        public int ConsumeRelease()
        {
            var restoredHeightOffset = _clickStartHeightOffset;
            _clickStartPosition = null;
            return restoredHeightOffset;
        }
    }
}
