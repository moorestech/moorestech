using Client.Input;
using Game.Block.Interface;
using UnityEngine;

namespace Client.Game.InGame.BlockSystem.PlaceSystem.Common.Input
{
    /// <summary>
    ///     回転キー入力から通常設置の次の向きを決める
    ///     Resolves normal placement direction from rotation key input
    /// </summary>
    internal static class PlacementDirectionKeyInput
    {
        internal static BlockDirection Apply(BlockDirection direction)
        {
            // 回転入力があるフレームだけ水平回転する
            // Rotate horizontally only on a rotation key press
            if (InputManager.Playable.BlockPlaceRotation.GetKeyDown)
            {
                direction = direction.HorizonRotation();

                // Shift併用時は水平回転後に鉛直方向も回転する
                // With Shift held, also rotate vertically after the horizontal turn
                if (HybridInput.GetKey(KeyCode.LeftShift)) direction = direction.VerticalRotation();
            }
            return direction;
        }
    }
}
