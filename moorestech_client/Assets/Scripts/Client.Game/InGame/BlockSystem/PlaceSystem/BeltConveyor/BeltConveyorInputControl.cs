using Client.Input;
using Game.Block.Interface;
using UnityEngine;

namespace Client.Game.InGame.BlockSystem.PlaceSystem.BeltConveyor
{
    /// <summary>
    /// 高さオフセットと設置方向のキー入力処理
    /// Key-input handling for height offset and placement direction
    /// </summary>
    public static class BeltConveyorInputControl
    {
        public static int AdjustHeightOffset(int heightOffset)
        {
            //TODO InputManagerに移す
            if (UnityEngine.Input.GetKeyDown(KeyCode.Q)) return heightOffset - 1;
            if (UnityEngine.Input.GetKeyDown(KeyCode.E)) return heightOffset + 1;
            return heightOffset;
        }

        public static BlockDirection RotateDirection(BlockDirection currentDirection)
        {
            if (!InputManager.Playable.BlockPlaceRotation.GetKeyDown) return currentDirection;

            //TODo シフトはインプットマネージャーに入れる
            if (UnityEngine.Input.GetKey(KeyCode.LeftShift)) return currentDirection.VerticalRotation();

            // 東西南北の向きを変更する
            // rotate through the four horizontal directions
            return currentDirection.HorizonRotation();
        }
    }
}
