namespace Client.Ui.Components.Slots
{
    /// <summary>
    /// SlotFrame の 5 状態（Web の data-selected・filled・catalog・insufficient・ghost）
    /// The five SlotFrame states (the Web data-selected, filled, catalog, insufficient and ghost)
    /// </summary>
    public readonly struct SlotFrameState
    {
        public readonly bool Selected;
        public readonly bool Filled;
        public readonly bool Catalog;
        public readonly bool Insufficient;
        public readonly bool Ghost;

        public SlotFrameState(bool selected, bool filled, bool catalog, bool insufficient, bool ghost)
        {
            Selected = selected;
            Filled = filled;
            Catalog = catalog;
            Insufficient = insufficient;
            Ghost = ghost;
        }
    }
}
