using Client.Input;
using Game.Block.Interface;

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

            // 傾斜は経路から解決するので回転キーは水平4方向だけを回す
            // Slopes come from the path, so the rotate key only cycles the four horizontal directions
            return currentDirection.HorizonRotation();
        }
    }
}
