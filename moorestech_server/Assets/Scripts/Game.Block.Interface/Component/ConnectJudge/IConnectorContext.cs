using System.Collections.Generic;
using Mooresmaster.Model.BlocksModule;
using UnityEngine;

namespace Game.Block.Interface.Component.ConnectJudge
{
    /// <summary>
    ///     コネクタ同士の接続可否を判定するドメイン固有ロジックの契約
    ///     Contract for domain-specific logic judging whether two connectors may connect
    /// </summary>
    public interface IConnectorContext<TTarget> where TTarget : IBlockComponent
    {
        List<Vector3Int> InitializeAndGetOverridelSubsrcibePositions(IBlockConnectorComponent<TTarget> component, BlockPositionInfo positionInfo);
        
        Dictionary<TTarget, ConnectedInfo> GetOverride(Dictionary<TTarget, ConnectedInfo> currentTarget, IBlock targetBlock);
        
        bool CanConnect(ConnectJudgeContext context);
    }

    public readonly struct ConnectJudgeContext
    {
        // コネクタは方向無制限（directions未設定）経路ではnullになり得る
        // Connectors may be null on the unrestricted-directions path
        public readonly IBlockConnector SelfConnector;
        public readonly IBlockConnector TargetConnector;
        public readonly BlockPositionInfo SelfPositionInfo;
        public readonly BlockPositionInfo TargetPositionInfo;

        public ConnectJudgeContext(IBlockConnector selfConnector, IBlockConnector targetConnector, BlockPositionInfo selfPositionInfo, BlockPositionInfo targetPositionInfo)
        {
            SelfConnector = selfConnector;
            TargetConnector = targetConnector;
            SelfPositionInfo = selfPositionInfo;
            TargetPositionInfo = targetPositionInfo;
        }
    }
}
