using Game.Block.Interface;
using Game.Block.Interface.Component.WorldMutation;

namespace Game.Block.Component.ConnectionContext
{
    internal interface IConnectorContext
    {
        bool HandlesOverride(IBlock targetBlock);
        IBlockWorldMutation CaptureWorldMutation();
    }

    internal sealed class DefaultConnectorContext : IConnectorContext, IBlockWorldMutation
    {
        public bool HandlesOverride(IBlock targetBlock) => false;
        public IBlockWorldMutation CaptureWorldMutation() => this;
        public void ApplyAfterMutation() { }
    }
}
