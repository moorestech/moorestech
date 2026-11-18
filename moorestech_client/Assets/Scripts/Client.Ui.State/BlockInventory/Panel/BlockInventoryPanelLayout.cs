namespace Client.Ui.State.BlockInventory.Panel
{
    // パネルの大きさの 3 状態（判別子）。大型で本文の高さが自然＝列車、確定＝寄与の大型
    // The three panel sizes (discriminator): large with natural body = train, large with filled body = contributed large
    public enum BlockInventoryPanelSize
    {
        Natural,
        LargeNaturalBody,
        LargeFilledBody,
    }

    /// <summary>
    /// パネルの大きさ（Web の isLargePanel と isLargeMachinePanel＝本文の高さ確定）。作れるのは 3 つの static だけで (小型, 高さ確定) は作れない
    /// Panel size (Web isLargePanel, and isLargeMachinePanel as the body's fixed height); only the three statics exist, so (small, filled) cannot be made
    /// </summary>
    public readonly struct BlockInventoryPanelLayout
    {
        public static readonly BlockInventoryPanelLayout Natural = new(BlockInventoryPanelSize.Natural);
        public static readonly BlockInventoryPanelLayout LargeNaturalBody = new(BlockInventoryPanelSize.LargeNaturalBody);
        public static readonly BlockInventoryPanelLayout LargeFilledBody = new(BlockInventoryPanelSize.LargeFilledBody);

        public readonly BlockInventoryPanelSize Size;
        public bool IsLarge => Size != BlockInventoryPanelSize.Natural;
        public bool FillsPanelHeight => Size == BlockInventoryPanelSize.LargeFilledBody;

        private BlockInventoryPanelLayout(BlockInventoryPanelSize size)
        {
            Size = size;
        }
    }

    /// <summary>
    /// パネルの状態と大きさの組。LayoutModel が 1 回の再計算で配り、画面はこれと画面ルータの 2 源だけを購読する（派生元と派生先を同時に購読すると途中の組が流れる。D-55）
    /// A pair of panel state and size; the layout model publishes it per recompute and the screen subscribes only to it and the screen router (subscribing to both source and derived value lets intermediate pairs through; D-55)
    /// </summary>
    public sealed class BlockInventoryPanelPresentation
    {
        public BlockInventoryPanelState State { get; private set; }
        public BlockInventoryPanelLayout Layout { get; private set; }

        public BlockInventoryPanelPresentation(BlockInventoryPanelState state, BlockInventoryPanelLayout layout)
        {
            State = state;
            Layout = layout;
        }
    }
}
