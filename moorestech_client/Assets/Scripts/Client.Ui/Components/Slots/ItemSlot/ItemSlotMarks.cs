namespace Client.Ui.Components.Slots
{
    /// <summary>
    /// 画面が決める ItemSlot の印。filled と ghost は中身から ItemSlot が決めるので持たない
    /// ItemSlot marks the screen decides; filled and ghost are derived from the content by ItemSlot, so they are absent
    /// </summary>
    public readonly struct ItemSlotMarks
    {
        public readonly bool Selected;
        public readonly bool Catalog;
        public readonly bool Insufficient;

        public ItemSlotMarks(bool selected, bool catalog, bool insufficient)
        {
            Selected = selected;
            Catalog = catalog;
            Insufficient = insufficient;
        }
    }
}
