using Game.BeltSegment;
using Game.Block.Interface.Component;
namespace Game.Block.Blocks.BeltConveyor
{
    internal sealed class BeltMachineReceiver : IBeltReceiver
    {
        private readonly IBlockInventory _target;
        private readonly InsertItemContext _context;
        private readonly BeltWorldItems _items;
        internal bool Consumed { get; private set; }
        internal BeltMachineReceiver(IBlockInventory target, InsertItemContext context, BeltWorldItems items)
        { _target = target; _context = context; _items = items; }
        internal void Reset() => Consumed = false;
        public void AttachInput(IBeltSource source, BeltDirection inputDirection) { }
        public int GetOffer(BeltDirection inputDirection) => Consumed ? 0 : BeltConstants.ItemWidth;
        public bool TryReceive(BeltDirection inputDirection, int length, in BeltItem item)
        {
            if (Consumed) return false;
            var remainder = _target.InsertItem(_items.Payload(item.Guid), _context);
            if (remainder.Count != 0) return false;
            _items.Remove(item.Guid);
            Consumed = true;
            return true;
        }
    }
}
