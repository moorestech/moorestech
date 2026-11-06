namespace Client.Ui.Components.Slots
{
    /// <summary>
    /// スロットの身振り 1 つ。Shift は左押下でだけ意味を持つ（Web の onLeftDown(shiftKey)）
    /// One slot gesture; Shift matters only for a left press (the Web onLeftDown(shiftKey))
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
