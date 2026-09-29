using Game.Block.Interface;
using Game.Block.Interface.Component;
using Game.Block.Interface.Component.WorldMutation;

namespace Game.Block.Component.ConnectionContext
{
    internal interface IConnectorContext<TTarget> where TTarget : IBlockComponent
    {
        bool HandlesOverride(IBlock targetBlock);
        IBlockWorldMutation CaptureWorldMutation();
    }

    internal sealed class DefaultConnectorContext<TTarget> : IConnectorContext<TTarget>, IBlockWorldMutation where TTarget : IBlockComponent
    {
        public bool HandlesOverride(IBlock targetBlock) => false;
        public IBlockWorldMutation CaptureWorldMutation() => this;
        public void ApplyAfterMutation() { }
    }
}
