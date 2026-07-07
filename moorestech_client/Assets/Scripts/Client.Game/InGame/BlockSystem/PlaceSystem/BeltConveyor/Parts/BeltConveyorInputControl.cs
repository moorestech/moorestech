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
            // CommonBlockPlaceSystemはShift押下時HorizonRotationとVerticalRotationを共に発火する潜在バグを持つが、
            // ベルトの向きは経路(直線/カーブ/傾斜)から自動解決するため、意図的にVerticalRotationのみへ限定する
            // CommonBlockPlaceSystem fires both HorizonRotation and VerticalRotation when Shift is held (a latent bug);
            // belt direction is auto-resolved from the path, so we intentionally restrict Shift+rotate to VerticalRotation only
            if (UnityEngine.Input.GetKey(KeyCode.LeftShift)) return currentDirection.VerticalRotation();

            // 東西南北の向きを変更する
            // rotate through the four horizontal directions
            return currentDirection.HorizonRotation();
        }
    }
}
