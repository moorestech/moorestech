using Client.Input;
using Game.Block.Interface;

namespace Client.Game.InGame.BlockSystem.PlaceSystem.Common.Input
{
    /// <summary>
    ///     回転キー入力から通常設置の次の向きを決める（水平4方向のみ）
    ///     Resolves normal placement direction from rotation key input (horizontal four directions only)
    /// </summary>
    internal static class PlacementDirectionKeyInput
    {
        internal static BlockDirection Apply(BlockDirection direction)
        {
            // 回転入力時のみ水平回転（縦倒し無し）
            // Rotate horizontally only on key press (never tips over)
            if (InputManager.Playable.BlockPlaceRotation.GetKeyDown) direction = direction.HorizonRotation();
            return direction;
        }
    }
}
