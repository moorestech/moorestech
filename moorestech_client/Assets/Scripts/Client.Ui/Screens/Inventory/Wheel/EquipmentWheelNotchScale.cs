using Client.Ui.Foundation.Input;

namespace Client.Ui.Screens.Inventory.Wheel
{
    /// <summary>
    /// InputSystem のノッチを Web の px へ換算
    /// Converts InputSystem notches to Web pixels
    /// </summary>
    public static class EquipmentWheelNotchScale
    {
        public static double ToDownwardPixels(float notchesUp)
        {
            return -notchesUp * WebWheelScale.PixelsPerNotch;
        }
    }
}
