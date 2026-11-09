namespace Client.Ui.Components.Slots
{
    /// <summary>
    /// 量の帯を出すかどうか。出さない側は量を持たない（Web の AmountBadge）
    /// Whether the amount band shows; the hidden side carries no amount (the Web AmountBadge)
    /// </summary>
    public abstract class SlotAmountBadge
    {
        public static readonly SlotAmountBadge Hidden = new HiddenSlotAmountBadge();

        private protected SlotAmountBadge()
        {
        }

        // 0 も 0 と描く
        // Zero draws as 0
        public static ShownSlotAmountBadge Show(double amount)
        {
            return new ShownSlotAmountBadge(amount);
        }
    }

    public sealed class HiddenSlotAmountBadge : SlotAmountBadge
    {
        internal HiddenSlotAmountBadge()
        {
        }
    }

    public sealed class ShownSlotAmountBadge : SlotAmountBadge
    {
        public double Amount { get; }

        internal ShownSlotAmountBadge(double amount)
        {
            Amount = amount;
        }
    }
}
