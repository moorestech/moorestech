using Client.Input;
using UnityEngine;

namespace Client.Game.InGame.BlockSystem.PlaceSystem.Common.Height
{
    /// <summary>
    ///     Q/Eを設置高さの増減へ変換する
    ///     Converts Q/E input to placement-height changes
    /// </summary>
    public static class PlacementHeightKeyInput
    {
        public static void Apply(PlacementHeightOffset heightOffset)
        {
            if (HybridInput.GetKeyDown(KeyCode.Q)) heightOffset.Adjust(-1);
            else if (HybridInput.GetKeyDown(KeyCode.E)) heightOffset.Adjust(1);
        }
    }
}
