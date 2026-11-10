using System;
using Client.Ui.Content;
using Client.Ui.Foundation.Layers;
using Client.Ui.Foundation.Root;
using Client.Ui.Foundation.Stage;
using Client.Ui.Screens.Inventory.Parts;
using Client.Ui.State.Inventory.Panel;
using Client.Ui.State.Routing;
using Client.Ui.State.SlotCommands.Player;
using Client.Ui.State.Toast;
using UniRx;
using VContainer.Unity;

namespace Client.Ui.Screens.Inventory.Equipment
{
    /// <summary>
    /// 装備 HUD を Chrome 層へ載せ、掴める画面でだけクリックを受ける（A-57）
    /// Mounts the equipment HUD on the chrome layer and clicks only on grab screens (A-57)
    /// </summary>
    public sealed class EquipmentHudPresenter : IInitializable, IDisposable
    {
        private readonly GameUiRoot _root;
        private readonly UiRoutingState _routing;
        private readonly PlayerInventoryPanelModel _model;
        private readonly EquipmentHudView _view;
        private IDisposable _routingSubscription;
        private IDisposable _modelSubscription;

        public EquipmentHudPresenter(GameUiRoot root, UiRoutingState routing, PlayerInventoryPanelModel model, IPlayerSlotCommands commands, GameContentPresentation content, UiCommandResultToaster toaster)
        {
            _root = root;
            _routing = routing;
            _model = model;
            _view = new EquipmentHudView(new PlayerSlotGestureRouter(commands, toaster), content);
        }

        public void Initialize()
        {
            _root.GetLayer(UiLayer.Chrome, UiFamily.Viewport).Add(_view);
            _routingSubscription = _routing.Current.Subscribe(snapshot => Apply(UiScreenTraitsTable.Get(snapshot.Screen)));

            #region Internal

            void Apply(UiScreenTraits traits)
            {
                var shown = traits.AlwaysOnHud;
                _view.SetClicksAccepted(traits.Grab);
                _view.SetShown(shown);
                if (shown && _modelSubscription == null) _modelSubscription = _model.Current.Subscribe(_view.Render);
                if (shown || _modelSubscription == null) return;
                _modelSubscription.Dispose();
                _modelSubscription = null;
            }

            #endregion
        }

        public void Dispose()
        {
            _routingSubscription?.Dispose();
            _modelSubscription?.Dispose();
            _view.RemoveFromHierarchy();
        }
    }
}
