using Client.Ui.State.Text;

namespace Client.Ui.Components.Slots
{
    /// <summary>
    /// 所持/必要の表示。赤字は Lacking、枠の減光は ItemSlotMarks.Insufficient と分ける（Web の Shortage）
    /// Owned/required display; red text follows Lacking while frame dimming follows ItemSlotMarks.Insufficient (the Web Shortage)
    /// </summary>
    public sealed class ItemSlotShortage
    {
        public double OwnedCount { get; }
        public double RequiredCount { get; }
        public bool Lacking { get; }

        // null は素材の語彙で語らない用途（Web の tooltipKey: null）。アイテム名のツールチップのままにする
        // null means a usage that must not speak in material vocabulary (the Web tooltipKey: null); the item-name tooltip stays
        public UiText Tooltip { get; }

        public ItemSlotShortage(double ownedCount, double requiredCount, bool lacking, UiText tooltip)
        {
            OwnedCount = ownedCount;
            RequiredCount = requiredCount;
            Lacking = lacking;
            Tooltip = tooltip;
        }
    }
}
