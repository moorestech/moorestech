using Client.Ui.Foundation.Input;

namespace Client.Ui.Screens.Inventory.Wheel
{
    /// <summary>
    /// InputSystem のノッチをホイールの px へ換算
    /// Converts InputSystem notches to wheel pixels
    /// </summary>
    public static class EquipmentWheelNotchScale
    {
        public static double ToDownwardPixels(float notchesUp)
        {
            return -notchesUp * WheelNotchPixels.PixelsPerNotch;
        }
    }
}
