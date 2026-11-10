using System;
using Client.Ui.Content;
using Client.Ui.Foundation.Stage.Columns;
using Client.Ui.Screens.Inventory.Parts;
using Client.Ui.State.Inventory.Panel;
using Client.Ui.State.Routing;
using Client.Ui.State.SlotCommands.Player;
using Client.Ui.State.Toast;
using UniRx;
using VContainer.Unity;

namespace Client.Ui.Screens.Inventory.Panel
{
    /// <summary>
    /// 持ち物パネルを舞台の Inv 列へ 1 回載せ、掴める画面の間だけ Model を購読して描く（A-92）
    /// Mounts the inventory panel once in the stage Inv column and subscribes to the model only while a grab screen shows (A-92)
    /// </summary>
    public sealed class InventoryScreenPresenter : IInitializable, IDisposable
    {
        private readonly StageColumnsLayout _columns;
        private readonly UiRoutingState _routing;
        private readonly PlayerInventoryPanelModel _model;
        private readonly InventoryView _view;
        private IDisposable _routingSubscription;
        private IDisposable _modelSubscription;

        public InventoryScreenPresenter(StageColumnsLayout columns, UiRoutingState routing, PlayerInventoryPanelModel model, IPlayerSlotCommands commands, IInventorySortCommands sort, GameContentPresentation content, UiCommandResultToaster toaster)
        {
            _columns = columns;
            _routing = routing;
            _model = model;
            _view = new InventoryView(new PlayerSlotGestureRouter(commands, toaster), sort, content, toaster);
        }

        public void Initialize()
        {
            _columns.Get(StageColumn.Inv).Add(_view);
            _routingSubscription = _routing.Current.Subscribe(snapshot => Apply(IsShownOn(snapshot.Screen)));

            #region Internal

            // 掴める画面だけに出す（Web screenAllowsGrab(screen) && <InventoryPanel/>。traits の列を読むのはこの 1 か所。前例 PauseMenuScreenPresenter）
            // Shown only on grab screens (Web screenAllowsGrab(screen) && <InventoryPanel/>; the one reader of this traits column, precedent PauseMenuScreenPresenter)
            bool IsShownOn(UiScreen screen)
            {
                return UiScreenTraitsTable.Get(screen).Grab;
            }

            #endregion
        }

        public void Dispose()
        {
            _routingSubscription?.Dispose();
            _modelSubscription?.Dispose();
            _view.RemoveFromHierarchy();
        }

        private void Apply(bool shown)
        {
            // 出す間だけ購読し、隠すと購読を外す（§4.2）
            // Subscribe only while shown and drop the subscription when hidden (§4.2)
            _view.SetShown(shown);
            if (shown && _modelSubscription == null) _modelSubscription = _model.Current.Subscribe(_view.Render);
            if (shown || _modelSubscription == null) return;
            _modelSubscription.Dispose();
            _modelSubscription = null;
        }
    }
}
