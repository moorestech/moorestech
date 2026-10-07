using System.Collections.Generic;
using UnityEngine;

namespace Game.Block.Interface.Component.ConnectJudge
{
    // 通常の位置・形状判定に追加制約を持たない共有Context
    // Shared context without restrictions beyond ordinary position and shape matching
    public class DefaultContext<TTarget> : IConnectorContext<TTarget> where TTarget : IBlockComponent
    {
        public virtual List<Vector3Int> InitializeAndGetOverridelSubsrcibePositions(IBlockConnectorComponent<TTarget> component,
            BlockPositionInfo positionInfo, ConnectorContextData data) => new();

        public virtual Dictionary<TTarget, ConnectedInfo> GetOverride(Dictionary<TTarget, ConnectedInfo> currentTarget, IBlock targetBlock,
            ConnectorContextData data, IConnectorWorldLookup world, IBlock removingBlock,
            Dictionary<TTarget, ConnectedInfo> ordinaryTargets) => new(ordinaryTargets);

        public virtual bool CanConnect(ConnectJudgeContext context) => true;
    }
}
