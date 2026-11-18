using Client.Ui.Foundation.Input.OsCursor;
using Client.Ui.Foundation.Stage.Columns;
using Client.Ui.Screens.BlockInventory.Registry;
using Client.Ui.State.BlockInventory.Panel;
using Client.Ui.State.Routing;
using UniRx;
using VContainer.Unity;

namespace Client.Ui.Screens.BlockInventory
{
    /// <summary>
    /// 外殻を Viewer 列へ 1 回だけ載せ、画面ルータとパネルの状態・大きさで出し入れと描き直しをする（前例 PauseMenuScreenPresenter）
    /// Mounts the shell on the Viewer column once and shows/re-renders from the screen router, panel state and size (precedent PauseMenuScreenPresenter)
    /// </summary>
    public sealed class BlockInventoryScreenPresenter : IInitializable
    {
        private readonly StageColumnsLayout _stage;
        private readonly UiRoutingState _routing;
        private readonly BlockInventoryPanelLayoutModel _layout;
        private readonly BlockInventoryPartRegistry _parts;
        private readonly IBlockInventoryPanelCommands _commands;
        private readonly IUiOsCursor _osCursor;
        private readonly SerialDisposable _layoutSubscription = new();

        public BlockInventoryScreenPresenter(StageColumnsLayout stage, UiRoutingState routing, BlockInventoryPanelLayoutModel layout, BlockInventoryPartRegistry parts, IBlockInventoryPanelCommands commands, IUiOsCursor osCursor)
        {
            _stage = stage;
            _routing = routing;
            _layout = layout;
            _parts = parts;
            _commands = commands;
            _osCursor = osCursor;
        }

        public void Initialize()
        {
            var view = new BlockInventoryView(_stage, _commands, _osCursor);

            var host = new BlockInventoryBodyHost(_parts, view);
            _stage.Get(StageColumn.Viewer).Add(view);

            // 画面ルータが SubInventory の間だけ LayoutModel の組 1 本を購読する（§4.2 の ①。派生元と派生先を同時に購読しない。D-55）
            // Follow the layout model's single pair only while the router is on SubInventory (§4.2 type 1; never both source and derived; D-55)
            _routing.Current.Select(BlockInventoryScreenVisibility.IsOnScreen).DistinctUntilChanged().Subscribe(onScreen =>
            {
                if (onScreen)
                {
                    _layoutSubscription.Disposable = _layout.Current.Subscribe(presentation => Apply(presentation.State, presentation.Layout));
                    return;
                }

                // 隠れたら購読を捨て、部分を 1 回外す（隠れている間は描かない）
                // On hide drop the subscription and detach the parts once (nothing is drawn while hidden)
                _layoutSubscription.Disposable = Disposable.Empty;
                view.SetShown(false);
                host.Apply(BlockInventoryPanelState.Closed, _layout.Current.Value.Layout);
            });

            #region Internal

            void Apply(BlockInventoryPanelState state, BlockInventoryPanelLayout layout)
            {
                var shown = BlockInventoryScreenVisibility.IsShown(_routing.Current.Value, state);
                view.SetShown(shown);
                view.Render(state, layout);
                host.Apply(shown ? state : BlockInventoryPanelState.Closed, layout);
            }

            #endregion
        }
    }
}
