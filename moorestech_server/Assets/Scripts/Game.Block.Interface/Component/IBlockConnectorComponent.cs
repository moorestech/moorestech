using System.Collections.Generic;
using Mooresmaster.Model.BlockConnectInfoModule;

namespace Game.Block.Interface.Component
{
    public interface IBlockConnectorComponent<TTarget> : IBlockComponent where TTarget : IBlockComponent
    {
        public IReadOnlyDictionary<TTarget, ConnectedInfo> ConnectedTargets { get; }
    }
    
    public struct ConnectedInfo
    {
        public IConnectOption SelfOption { get; }
        public IConnectOption TargetOption { get; }
        
        public IBlock TargetBlock { get; }
        
        public ConnectedInfo(IConnectOption selfOption, IConnectOption targetOption, IBlock targetBlock)
        {
            SelfOption = selfOption;
            TargetOption = targetOption;
            TargetBlock = targetBlock;
        }
    }
}