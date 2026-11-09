using Client.Ui.Screens.BlockInventory.Registry;
using Client.Ui.State.BlockInventory.Panel;

namespace Client.Ui.Screens.BlockInventory
{
    /// <summary>
    /// 本文・題の操作・列車の本文の Presenter を付け替える。同じパネル（IsSamePanel）かつ同じ大きさのあいだは何もしない（各部分が自分で購読している）
    /// Swaps the body, title-action and train-body presenters; does nothing while the panel (IsSamePanel) and size stay the same (each part follows its own subscriptions)
    /// </summary>
    public sealed class BlockInventoryBodyHost
    {
        private readonly BlockInventoryPartRegistry _parts;
        private readonly BlockInventoryView _view;
        private IBlockInventoryPart _body;
        private IBlockInventoryPart _titleAction;
        private ITrainInventoryPart _trainBody;
        private BlockInventoryPanelState _state = BlockInventoryPanelState.Closed;
        private BlockInventoryPanelLayout _layout = BlockInventoryPanelLayout.Natural;

        public BlockInventoryBodyHost(BlockInventoryPartRegistry parts, BlockInventoryView view)
        {
            _parts = parts;
            _view = view;
        }

        public void Apply(BlockInventoryPanelState state, BlockInventoryPanelLayout layout)
        {
            // 作り直すかは State の IsSamePanel と大きさの 2 つだけで決める
            // Rebuild is decided only by the State's IsSamePanel and the size
            var samePanel = state.IsSamePanel(_state);
            if (samePanel && layout.Size == _layout.Size) return;
            _state = state;
            _layout = layout;
            DetachAll();

            // 別のパネル・エラーからの復帰では先頭から始める（Web の key={data.identifier} による再マウント）。同じパネルの大きさ変更では位置を保つ
            // A different panel or recovery from an error starts from the top (Web key={data.identifier} remount); a resize of the same panel keeps the position
            if (!samePanel) _view.RestartBodyScroll();

            // エラー中は何も載せない（Web の !error）。小型は自然高のスクロール器で包む（Web の !isLarge）
            // Nothing is mounted while erroring (Web !error); natural panels wrap the body in the natural-height scroller (Web !isLarge)
            if (state is OpenBlockInventoryPanel block && block.Error == BlockInventoryPanelError.None)
            {
                _body = _parts.CreateBody(block, new BlockInventoryBodyMount(layout.FillsPanelHeight));
                _view.PlaceBody(_body.Root, !layout.IsLarge);
                _body.Attach(block);
                _titleAction = _parts.CreateTitleAction(block.Components.TitleAction);
                _view.PlaceTitleAction(_titleAction.Root);
                _titleAction.Attach(block);
            }

            // 列車は常に大型（LayoutModel が決める）
            // Trains are always large (decided by the layout model)
            if (state is OpenTrainInventoryPanel train && train.Error == BlockInventoryPanelError.None)
            {
                _trainBody = _parts.CreateTrainBody();
                _view.PlaceBody(_trainBody.Root, !layout.IsLarge);
                _trainBody.Attach(train);
            }

            #region Internal

            void DetachAll()
            {
                _body?.Detach();
                _titleAction?.Detach();
                _trainBody?.Detach();
                _body = null;
                _titleAction = null;
                _trainBody = null;
                _view.ClearBody();
                _view.ClearTitleAction();
            }

            #endregion
        }
    }
}
