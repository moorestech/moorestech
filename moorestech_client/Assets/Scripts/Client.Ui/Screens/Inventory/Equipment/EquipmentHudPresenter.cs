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
    /// 装備 HUD を Viewport の Chrome 層へ 1 回載せ、常駐 HUD の画面で出し、掴める画面でだけクリックを受ける（A-57）
    /// Mounts the equipment HUD once on the Viewport chrome layer, shows it on always-on-HUD screens and accepts clicks only on grab screens (A-57)
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
        }

        public void Dispose()
        {
            _routingSubscription?.Dispose();
            _modelSubscription?.Dispose();
            _view.RemoveFromHierarchy();
        }

        private void Apply(UiScreenTraits traits)
        {
            // 常駐 HUD の画面で出し（blocking スキットでも隠さない）、掴める画面でだけクリックを受ける（Web screenShowsAlwaysOnHud・useGrabInteractive）
            // Show on always-on-HUD screens (even during a blocking skit) and take clicks only on grab screens (Web screenShowsAlwaysOnHud, useGrabInteractive)
            var shown = traits.AlwaysOnHud;
            _view.SetClicksAccepted(traits.Grab);
            _view.SetShown(shown);
            if (shown && _modelSubscription == null) _modelSubscription = _model.Current.Subscribe(_view.Render);
            if (shown || _modelSubscription == null) return;
            _modelSubscription.Dispose();
            _modelSubscription = null;
        }
    }
}
