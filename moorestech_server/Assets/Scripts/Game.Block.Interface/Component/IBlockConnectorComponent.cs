using System.Collections.Generic;
using Mooresmaster.Model.BlocksModule;
using UnityEngine;

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

        // 方向指定のない入力も実際の接続セルを保持する。
        // Keep the matched cell even when the input connector is directionless.
        public Vector3Int TargetConnectorCell { get; }

        public ConnectedInfo(IBlockConnector selfConnector, IBlockConnector targetConnector, IBlock targetBlock, Vector3Int targetConnectorCell)
        {
            SelfConnector = selfConnector;
            TargetConnector = targetConnector;
            TargetBlock = targetBlock;
            TargetConnectorCell = targetConnectorCell;
        }
    }
}
