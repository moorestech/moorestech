using System.Collections.Generic;

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
        public IBlockConnector SelfConnector { get; }

        /// <summary>
        /// 接続先のコネクター情報
        /// Connector information on target side
        /// </summary>
        public IBlockConnector TargetConnector { get; }

        public IBlock TargetBlock { get; }

        public ConnectedInfo(IBlockConnector selfConnector, IBlockConnector targetConnector, IBlock targetBlock)
        {
            SelfConnector = selfConnector;
            TargetConnector = targetConnector;
            TargetBlock = targetBlock;
        }
    }
}
