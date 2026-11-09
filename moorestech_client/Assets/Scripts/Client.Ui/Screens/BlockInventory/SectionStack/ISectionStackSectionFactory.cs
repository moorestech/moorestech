namespace Client.Ui.Screens.BlockInventory.SectionStack
{
    /// <summary>
    /// 拡張口 (3): 固定順の区画への差し込み（1 Slot に 1 本）
    /// Extension port (3): plugging into a fixed-order section (one per slot)
    /// </summary>
    public interface ISectionStackSectionFactory
    {
        SectionStackSlot Slot { get; }
        IBlockInventorySection Create(SectionStackSectionMount mount);
    }
}
