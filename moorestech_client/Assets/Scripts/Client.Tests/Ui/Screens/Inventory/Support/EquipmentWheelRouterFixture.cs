using Client.Game.InGame.BlockSystem.PlaceSystem;
using Client.Game.InGame.BlockSystem.PlaceSystem.Common;
using Client.Game.InGame.BlockSystem.PlaceSystem.Empty;
using Client.Game.InGame.UI.UIState;
using Client.Tests.PlaceSystem.Common.Height;
using Client.Tests.Ui.State.Inventory.Equipment;
using Client.Ui.State.Inventory.Equipment;
using Client.Ui.State.Modal;
using UnityEngine;

namespace Client.Tests.Ui.Screens.Inventory.Support
{
    /// <summary>
    /// 装備のホイールのルーターを本物の画面・モーダル・配置の State で組む
    /// Builds the equipment wheel router on real screen, modal and placement State
    /// </summary>
    public sealed class EquipmentWheelRouterFixture
    {
        public ModalRequestService Modal { get; private set; }
        public PlaceSystemStateController Placement { get; private set; }
        public EquipmentWheelRouter Router { get; private set; }

        public EquipmentWheelRouterFixture(GameObject host, UIStateEnum state, FakeEquipmentSelectionPort port)
        {
            var selector = new EquipmentWheelSelector(port);
            selector.Initialize();
            Modal = new ModalRequestService();
            Placement = new PlaceSystemStateController(new PlacementHeightWiringFakes.SwitchableSelector(new EmptyPlaceSystem()), new PlacementHeightWiringFakes.NullPresenter(), new PlacementHeightOffset());
            Router = new EquipmentWheelRouter(selector, UiRoutingStateTestFactory.Create(host, state), Modal, Placement);
        }
    }
}
