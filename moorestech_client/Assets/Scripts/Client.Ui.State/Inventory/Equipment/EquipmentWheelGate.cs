using Client.Ui.State.Routing;

namespace Client.Ui.State.Inventory.Equipment
{
    public enum EquipmentWheelBlock
    {
        None,
        Layer,
        Ui,
        Tool,
    }

    /// <summary>
    /// ホイール抑止の判定（Web の順に評価）
    /// Wheel suppression check (evaluated in Web order)
    /// </summary>
    public static class EquipmentWheelGate
    {
        public static EquipmentWheelBlock Judge(EquipmentWheelSituation situation)
        {
            // 前面の層がゲームでなければ止める（Web deriveActiveLayer: modal > blockInventory > research > buildMenu）
            // Stop unless the front layer is the game (Web deriveActiveLayer: modal > blockInventory > research > buildMenu)
            if (situation.ModalPending) return EquipmentWheelBlock.Layer;
            if (situation.Screen == UiScreen.SubInventory || situation.Screen == UiScreen.ResearchTree || situation.Screen == UiScreen.BuildMenu) return EquipmentWheelBlock.Layer;

            // UI の上のホイールはその画面の操作。占有は C# の値をそのまま読む
            // A wheel over UI belongs to that screen; ownership is read as C# publishes it
            if (situation.OverBlockingUi) return EquipmentWheelBlock.Ui;
            if (situation.ToolOwnsWheel) return EquipmentWheelBlock.Tool;
            return EquipmentWheelBlock.None;
        }
    }
}
