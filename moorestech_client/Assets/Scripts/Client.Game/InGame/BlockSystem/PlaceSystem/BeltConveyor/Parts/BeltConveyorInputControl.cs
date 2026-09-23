using Client.Input;
using Game.Block.Interface;
using UnityEngine;

namespace Client.Game.InGame.BlockSystem.PlaceSystem.BeltConveyor.Parts
{
    /// <summary>
    /// 高さオフセットと設置方向のキー入力処理
    /// Key-input handling for height offset and placement direction
    /// </summary>
    public static class BeltConveyorInputControl
    {
        public static BlockDirection RotateDirection(BlockDirection currentDirection)
        {
            if (!InputManager.Playable.BlockPlaceRotation.GetKeyDown) return currentDirection;

            // 東西南北の向きを変更する
            // rotate through the four horizontal directions
            return currentDirection.HorizonRotation();
        }
    }
}
