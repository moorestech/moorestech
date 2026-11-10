using Client.Ui.State.Routing;

namespace Client.Ui.State.Inventory.Equipment
{
    /// <summary>
    /// ホイールを装備へ流してよいかの入力
    /// Inputs deciding whether the wheel may switch equipment
    /// </summary>
    public readonly struct EquipmentWheelSituation
    {
        public readonly UiScreen Screen;
        public readonly bool ModalPending;
        public readonly bool OverBlockingUi;
        public readonly bool ToolOwnsWheel;

        public EquipmentWheelSituation(UiScreen screen, bool modalPending, bool overBlockingUi, bool toolOwnsWheel)
        {
            Screen = screen;
            ModalPending = modalPending;
            OverBlockingUi = overBlockingUi;
            ToolOwnsWheel = toolOwnsWheel;
        }
    }
}
