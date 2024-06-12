using System.Collections.Generic;
using Game.Block.Interface.BlockConfig;

namespace Game.Block.Interface.Component
{
    public interface IBlockConnectorComponent<TTarget> : IBlockComponent where TTarget : IBlockComponent
    {
        public IReadOnlyDictionary<TTarget, (IConnectOption selfOption, IConnectOption targetOption)> ConnectedTargets { get; }
    }
}