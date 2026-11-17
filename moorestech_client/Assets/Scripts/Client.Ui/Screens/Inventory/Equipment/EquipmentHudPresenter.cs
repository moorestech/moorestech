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
    public sealed class EquipmentHudPresenter : IInitializable
    {
        private readonly GameUiRoot _root;
        private readonly UiRoutingState _routing;
        private readonly PlayerInventoryPanelModel _model;
        private readonly EquipmentHudView _view;
        private readonly SerialDisposable _modelSubscription = new();

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
            _routing.Current.Subscribe(snapshot => _view.SetClicksAccepted(UiScreenTraitsTable.Get(snapshot.Screen).Grab));
            _routing.Current.Select(snapshot => UiScreenTraitsTable.Get(snapshot.Screen).AlwaysOnHud).DistinctUntilChanged().Subscribe(SetShown);

            #region Internal

            // traits で出ている間だけモデルを購読する（§4.2 の ①）
            // Follow the model only while the traits show the HUD (§4.2 type 1)
            void SetShown(bool shown)
            {
                _view.SetShown(shown);
                _modelSubscription.Disposable = shown ? _model.Current.Subscribe(_view.Render) : Disposable.Empty;
            }

            #endregion
        }

    }
}
