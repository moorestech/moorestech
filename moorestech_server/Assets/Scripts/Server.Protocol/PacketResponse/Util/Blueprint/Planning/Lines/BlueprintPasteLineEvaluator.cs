using System.Collections.Generic;
using Core.Master;
using Game.Blueprint;
using Mooresmaster.Model.BlocksModule;
using Server.Protocol.PacketResponse.Util.ElectricWire.AutoConnect;
using Server.Protocol.PacketResponse.Util.ElectricWire.ConnectionRange;
using UnityEngine;
using Server.Protocol.PacketResponse.Util.GearChain;

namespace Server.Protocol.PacketResponse.Util.Blueprint.Planning
{
    internal sealed class BlueprintPasteLineEvaluator
    {
        private readonly Dictionary<(BlueprintPasteLineKind kind, int index), int> _counts = new();
        private readonly HashSet<(BlueprintPasteLineKind kind, int a, int b)> _pairs = new();

        internal BlueprintPasteLineFailureReason Evaluate(BlueprintPasteLineKind kind,
            BlueprintPlacementElement a, BlueprintPlacementElement b)
        {
            if (a.BlockIndex == b.BlockIndex) return BlueprintPasteLineFailureReason.InvalidTarget;
            var pair = (kind, Mathf.Min(a.BlockIndex, b.BlockIndex), Mathf.Max(a.BlockIndex, b.BlockIndex));
            if (_pairs.Contains(pair)) return BlueprintPasteLineFailureReason.AlreadyConnected;

            // 新設端点の予約接続だけを数える
            // Count only reserved connections on newly placed endpoints
            _counts.TryGetValue((kind, a.BlockIndex), out var countA);
            _counts.TryGetValue((kind, b.BlockIndex), out var countB);
            var reason = EvaluateEndpoints(kind, a, b, countA, countB);
            if (reason != BlueprintPasteLineFailureReason.None) return reason;
            _counts[(kind, a.BlockIndex)] = countA + 1;
            _counts[(kind, b.BlockIndex)] = countB + 1;
            _pairs.Add(pair);
            return BlueprintPasteLineFailureReason.None;
        }

        private static BlueprintPasteLineFailureReason EvaluateEndpoints(BlueprintPasteLineKind kind,
            BlueprintPlacementElement a, BlueprintPlacementElement b, int countA, int countB)
        {
            var paramA = MasterHolder.BlockMaster.GetBlockMaster(a.BlockId).BlockParam;
            var paramB = MasterHolder.BlockMaster.GetBlockMaster(b.BlockId).BlockParam;
            if (kind == BlueprintPasteLineKind.GearChain)
            {
                if (paramA is not GearChainPoleBlockParam chainA || paramB is not GearChainPoleBlockParam chainB)
                    return BlueprintPasteLineFailureReason.InvalidTarget;
                var reason = GearChainPlacementEvaluator.EvaluateConnection(Vector3Int.Distance(a.Position, b.Position),
                    chainA.MaxConnectionDistance, chainB.MaxConnectionDistance, false,
                    chainA.MaxConnectionCount <= countA || chainB.MaxConnectionCount <= countB);
                return reason switch
                {
                    GearChainPlacementFailureReason.TooFar => BlueprintPasteLineFailureReason.OutOfRange,
                    GearChainPlacementFailureReason.ConnectionLimit => BlueprintPasteLineFailureReason.ConnectionLimit,
                    _ => BlueprintPasteLineFailureReason.None,
                };
            }

            // 電線の範囲は通常接続と共有する
            // Share wire range rules with ordinary connections
            if (!ElectricWireBlockParamResolver.TryGetWireRangeParam(paramA, out var maxA, out var rangeA, out var poleA) ||
                !ElectricWireBlockParamResolver.TryGetWireRangeParam(paramB, out var maxB, out var rangeB, out var poleB))
                return BlueprintPasteLineFailureReason.InvalidTarget;
            if (!ElectricConnectionRangeService.IsMutuallyConnectable(BlueprintPlacementElementUtil.ToPositionInfo(a), rangeA, poleA,
                    BlueprintPlacementElementUtil.ToPositionInfo(b), rangeB, poleB))
                return BlueprintPasteLineFailureReason.OutOfRange;
            return maxA <= countA || maxB <= countB
                ? BlueprintPasteLineFailureReason.ConnectionLimit : BlueprintPasteLineFailureReason.None;
        }
    }
}
