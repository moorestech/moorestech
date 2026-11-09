namespace Client.Ui.Components.Slots
{
    /// <summary>
    /// 量の帯の表示可否。非表示側は量を持たない
    /// Whether the amount band shows; the hidden side carries no amount (the Web AmountBadge)
    /// </summary>
    public abstract class SlotAmountBadge
    {
        public static readonly SlotAmountBadge Hidden = new HiddenSlotAmountBadge();

        private protected SlotAmountBadge()
        {
        }

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
