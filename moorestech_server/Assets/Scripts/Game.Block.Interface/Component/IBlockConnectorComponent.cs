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
        /// <summary>
        /// 自分側のコネクター情報
        /// Connector information on self side
        /// </summary>
        public BlockConnectInfoElement SelfConnector { get; }

        /// <summary>
        /// 接続先のコネクター情報
        /// Connector information on target side
        /// </summary>
        public BlockConnectInfoElement TargetConnector { get; }

        public IBlock TargetBlock { get; }

        public ConnectedInfo(BlockConnectInfoElement selfConnector, BlockConnectInfoElement targetConnector, IBlock targetBlock)
        {
            SelfConnector = selfConnector;
            TargetConnector = targetConnector;
            TargetBlock = targetBlock;
        }
    }
}