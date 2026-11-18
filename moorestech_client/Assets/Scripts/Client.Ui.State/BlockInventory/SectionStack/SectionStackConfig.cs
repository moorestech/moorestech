namespace Client.Ui.State.BlockInventory.SectionStack
{
    /// <summary>
    /// 標準の本文の blockType ごとの設定。項目グリッドと液体の出し方をそれぞれ判別 union で持つ（Web SectionStackViewConfig の写し）
    /// Per-blockType standard-body settings; the item grid and fluids each use a discriminated union (port of Web SectionStackViewConfig)
    /// </summary>
    public sealed class SectionStackConfig
    {
        public SectionStackItemGrid ItemGrid { get; private set; }
        public SectionStackFluid Fluid { get; private set; }

        public SectionStackConfig(SectionStackItemGrid itemGrid, SectionStackFluid fluid)
        {
            ItemGrid = itemGrid;
            Fluid = fluid;
        }
    }
}
