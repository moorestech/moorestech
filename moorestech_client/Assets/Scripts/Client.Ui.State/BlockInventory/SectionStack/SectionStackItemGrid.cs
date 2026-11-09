namespace Client.Ui.State.BlockInventory.SectionStack
{
    // 項目グリッド: 出さない／出す（GridId は Web の itemGridTestId と同じ綴り。RenderEmpty は空でも出すか）
    // Item grid: hidden, or shown (GridId spells the Web itemGridTestId; RenderEmpty draws it even when empty)
    public abstract class SectionStackItemGrid
    {
        public static readonly SectionStackItemGrid Hidden = new HiddenSectionStackItemGrid();

        private protected SectionStackItemGrid()
        {
        }
    }

    internal sealed class HiddenSectionStackItemGrid : SectionStackItemGrid
    {
        internal HiddenSectionStackItemGrid()
        {
        }
    }

    public sealed class ShownSectionStackItemGrid : SectionStackItemGrid
    {
        public string GridId { get; private set; }
        public bool RenderEmpty { get; private set; }

        public ShownSectionStackItemGrid(string gridId, bool renderEmpty)
        {
            GridId = gridId;
            RenderEmpty = renderEmpty;
        }
    }
}
