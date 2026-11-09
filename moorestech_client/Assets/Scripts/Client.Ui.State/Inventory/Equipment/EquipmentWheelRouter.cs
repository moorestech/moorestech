using Client.Game.InGame.BlockSystem.PlaceSystem;
using Client.Ui.State.Inventory.Logic;
using Client.Ui.State.Modal;
using Client.Ui.State.Routing;

namespace Client.Ui.State.Inventory.Equipment
{
    /// <summary>
    /// 画面・モーダル・ツールの占有を State から読んでホイールを判定し、通れば装備の循環へ流す
    /// Reads screen, modal and tool ownership from State, judges the wheel, and passes it to equipment cycling when allowed
    /// </summary>
    public sealed class EquipmentWheelRouter
    {
        private readonly EquipmentWheelSelector _selector;
        private readonly UiRoutingState _routing;
        private readonly ModalRequestService _modal;
        private readonly PlaceSystemStateController _placement;

        public EquipmentWheelRouter(EquipmentWheelSelector selector, UiRoutingState routing, ModalRequestService modal, PlaceSystemStateController placement)
        {
            _selector = selector;
            _routing = routing;
            _modal = modal;
            _placement = placement;
        }

        public void Route(double delta, WheelDeltaUnit unit, bool overBlockingUi)
        {
            // UI の上かだけは Client.Ui がパネルで拾って決める（State は UIElements を知らない）
            // Only "over UI" is decided by Client.Ui picking on the panel (State does not know UIElements)
            var situation = new EquipmentWheelSituation(_routing.Current.Value.Screen, _modal.Pending != null, overBlockingUi, _placement.IsWheelOwnedByTool);

            // silent-ok: 止めたホイールはその画面かツールの操作で、装備に触れないのが正しい挙動 / L1 の EquipmentWheelFlowTest と録画が見る
            if (EquipmentWheelGate.Judge(situation) != EquipmentWheelBlock.None) return;
            _selector.Wheel(delta, unit);
        }
    }
}
