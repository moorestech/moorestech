namespace Client.Ui.Components.Slots
{
    /// <summary>
    /// 身振り 1 つ。Shift は左押下のみ有効
    /// One gesture; Shift matters only for a left press
    /// </summary>
    public readonly struct SlotGesture
    {
        public readonly SlotGestureKind Kind;
        public readonly bool Shift;

        public SlotGesture(SlotGestureKind kind, bool shift)
        {
            Kind = kind;
            Shift = shift;
        }
    }
}
