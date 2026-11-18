using Client.Ui.Foundation.Icons;

namespace Client.Ui.Components.Slots
{
    /// <summary>
    /// 空きマスに置く「入るべきアイテム」の半透明の見本（Web の ghost）
    /// Translucent preview of the item that belongs in an empty cell (the Web ghost)
    /// </summary>
    public sealed class ItemSlotGhost
    {
        public UiIcon Icon { get; }
        public int Count { get; }

        public ItemSlotGhost(UiIcon icon, int count)
        {
            Icon = icon;
            Count = count;
        }
    }
}
