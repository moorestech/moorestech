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

        public BlockInventoryScreenPresenter(StageColumnsLayout stage, UiRoutingState routing, BlockInventoryPanelLayoutModel layout, BlockInventoryPartRegistry parts, IBlockInventoryPanelCommands commands)
        {
            _stage = stage;
            _routing = routing;
            _layout = layout;
            _parts = parts;
            _commands = commands;
        }

        public void Initialize()
        {
            var view = new BlockInventoryView(_stage, _commands);
            var host = new BlockInventoryBodyHost(_parts, view);
            _stage.Get(StageColumn.Viewer).Add(view);

            // 状態と大きさは LayoutModel の組 1 本から読み、画面ルータとの 2 源だけを組にする（派生元と派生先を同時に購読しない。D-55）。隠れている間は部分を外す
            // Read state and size from the layout model's single pair and combine only it with the screen router (never both source and derived; D-55); parts are detached while hidden
            _routing.Current.CombineLatest(_layout.Current, (snapshot, presentation) => (snapshot, presentation)).Subscribe(pair =>
            {
                var state = pair.presentation.State;
                var layout = pair.presentation.Layout;
                var shown = BlockInventoryScreenVisibility.IsShown(pair.snapshot, state);
                view.SetShown(shown);
                view.Render(state, layout);
                host.Apply(shown ? state : BlockInventoryPanelState.Closed, layout);
            });
        }
    }
}
