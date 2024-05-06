using System.Collections.Generic;

namespace Game.Block.Interface.Component
{
    public interface IBlockConnectorComponent<out TTarget> : IBlockComponent where TTarget : IBlockComponent
    {
        public IReadOnlyList<TTarget> ConnectTargets { get; }
    }
}