using Game.BeltSegment;
using Game.Block.Interface.Component;
namespace Game.Block.Blocks.BeltConveyor
{
    internal sealed class BeltMachineSource : IBeltSource
    {
        private readonly IBlockOutputAvailability _owner;
        internal bool Ready { get; private set; }
        internal BeltMachineSource(IBlockOutputAvailability owner) => _owner = owner;
        internal void Freeze() => Ready = _owner.HasOutputItem();
        public bool TryGetOutput(BeltDirection direction) => Ready;
    }
}
