namespace Client.Ui.State.BlockInventory.SectionStack
{
    // 液体: 出さない／行（機械以外。ShowProgress は進捗を出すか）／詳細（Web showFluidDetails）
    // Fluids: hidden, a row (non-machines; ShowProgress draws progress), or details (Web showFluidDetails)
    public abstract class SectionStackFluid
    {
        public static readonly SectionStackFluid Hidden = new HiddenSectionStackFluid();

        private protected SectionStackFluid()
        {
        }
    }

    public sealed class HiddenSectionStackFluid : SectionStackFluid
    {
        internal HiddenSectionStackFluid()
        {
        }
    }

    public sealed class RowSectionStackFluid : SectionStackFluid
    {
        public string RowId { get; private set; }
        public bool ShowProgress { get; private set; }

        public RowSectionStackFluid(string rowId, bool showProgress)
        {
            RowId = rowId;
            ShowProgress = showProgress;
        }
    }

    public sealed class DetailsSectionStackFluid : SectionStackFluid
    {
        public string RowId { get; private set; }

        public DetailsSectionStackFluid(string rowId)
        {
            RowId = rowId;
        }
    }
}
