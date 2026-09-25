using System.Collections.Generic;
using Game.Block.Blocks.TrainRail;
using Game.Context;
using Game.Train.Diagram;
using Server.Util.MessagePack;

namespace Server.Protocol.PacketResponse
{
    // 要求の停車駅を検証し、ドメインの停車指定へ解決する
    // Validate the requested stops and resolve them into domain stop plans
    internal static class TrainScheduleStopResolver
    {
        // 1件でも解決できなければ全体を拒否し、理由と詳細を呼び出し側へ返す
        // A single unresolvable stop rejects the whole request, returning the reason and detail to the caller
        internal static bool TryResolve(
            List<TrainTimetableStopMessagePack> requestStops,
            out List<TrainDiagramStopPlan> stops,
            out TrainScheduleEditFailureReason failureReason,
            out string detail)
        {
            stops = new List<TrainDiagramStopPlan>();
            failureReason = TrainScheduleEditFailureReason.None;
            detail = string.Empty;

            if (requestStops == null || requestStops.Contains(null))
            {
                failureReason = TrainScheduleEditFailureReason.InvalidRequest;
                detail = "stops are missing";
                return false;
            }

            foreach (var stop in requestStops)
            {
                if (stop.StationPosition == null)
                {
                    failureReason = TrainScheduleEditFailureReason.InvalidRequest;
                    detail = "station position is missing";
                    return false;
                }

                var position = stop.StationPosition.Vector3Int;
                if (!TrainTimetableWireMapping.TryToStationNodeSide(stop.Side, out var side))
                {
                    failureReason = TrainScheduleEditFailureReason.InvalidStationSide;
                    detail = $"pos={position} side={(int)stop.Side}";
                    return false;
                }
                if (!TrainTimetableWireMapping.TryToDepartureConditionType(stop.DepartureCondition, out var departureConditionType))
                {
                    failureReason = TrainScheduleEditFailureReason.InvalidRequest;
                    detail = $"pos={position} departureCondition={(int)stop.DepartureCondition}";
                    return false;
                }
                if (stop.WaitTicks < 0)
                {
                    failureReason = TrainScheduleEditFailureReason.InvalidRequest;
                    detail = $"pos={position} waitTicks={stop.WaitTicks}";
                    return false;
                }

                var block = ServerContext.WorldBlockDatastore.GetBlock(position);
                if (block == null)
                {
                    failureReason = TrainScheduleEditFailureReason.StationBlockNotFound;
                    detail = $"pos={position}";
                    return false;
                }
                if (!TrainTimetableStationNodeResolver.TryResolve(block, side, out var node))
                {
                    failureReason = TrainScheduleEditFailureReason.NotTrainStation;
                    detail = $"pos={position} type={block.BlockMasterElement.BlockType}";
                    return false;
                }
                stops.Add(new TrainDiagramStopPlan(node, departureConditionType, stop.WaitTicks));
            }
            return true;
        }
    }
}
