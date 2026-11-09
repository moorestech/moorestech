using Client.Ui.Foundation.Input;

namespace Client.Ui.Screens.Inventory.Wheel
{
    /// <summary>
    /// InputSystem のノッチ（上が正）を Web の deltaY の px（下が正。A-71 の 1 ノッチ＝60px）へ直す
    /// Converts InputSystem notches (up positive) to Web deltaY pixels (down positive; A-71's 60px per notch)
    /// </summary>
    public static class EquipmentWheelNotchScale
    {
        public static double ToDownwardPixels(float notchesUp)
        {
            return -notchesUp * WebWheelScale.PixelsPerNotch;
        }
    }
}
