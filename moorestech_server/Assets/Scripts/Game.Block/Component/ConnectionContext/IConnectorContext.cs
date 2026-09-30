using System;
using Game.Block.Interface;
using Game.Block.Interface.Component;

namespace Game.Block.Component.ConnectionContext
{
    internal interface IConnectorContext<TTarget> : IDisposable where TTarget : IBlockComponent
    {
        bool HandlesOverride(IBlock targetBlock);
    }

    internal sealed class DefaultConnectorContext<TTarget> : IConnectorContext<TTarget> where TTarget : IBlockComponent
    {
        public bool HandlesOverride(IBlock targetBlock) => false;
        public void Dispose() { }
    }
}
