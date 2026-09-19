using Client.Input;
using Game.Block.Interface;
using UnityEngine;

namespace Client.Game.InGame.BlockSystem.PlaceSystem.Util
{
    /// <summary>
    ///     設置向きの回転キー入力。全設置系がこの1入口を通る
    ///     Rotation-key input for the placement direction; every placement system goes through this single entry
    /// </summary>
    public static class BlockPlaceRotationInput
    {
        public static BlockDirection RotateDirection(BlockDirection currentDirection)
        {
            if (!InputManager.Playable.BlockPlaceRotation.GetKeyDown) return currentDirection;

            //TODo シフトはインプットマネージャーに入れる
            return Rotate(currentDirection, HybridInput.GetKey(KeyCode.LeftShift));
        }

        public static BlockDirection Rotate(BlockDirection currentDirection, bool isVerticalModifierHeld)
        {
            // Shift+回転は上下回転だけにする。水平回転と同時に掛けると意図より90°余計に回る
            // Shift+rotate is the vertical rotation only; also applying the horizontal one would overshoot by 90 degrees
            if (isVerticalModifierHeld) return currentDirection.VerticalRotation();

            // 東西南北の向きを変更する
            // Rotate through the four horizontal directions
            return currentDirection.HorizonRotation();
        }
    }
}
