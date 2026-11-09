using Client.Ui.State.Routing;

namespace Client.Ui.State.Inventory.Equipment
{
    /// <summary>
    /// ホイールを装備へ流してよいかを決める入力。UI の上かは Client.Ui がパネルで拾って決める
    /// Inputs that decide whether the wheel may switch equipment; Client.Ui decides "over UI" by picking on the panel
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
