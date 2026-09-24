using System;
using System.Collections.Generic;
using Game.Block.Blocks.TrainRail;
using Game.Context;
using Game.Train.Event;
using Game.Train.RailGraph;
using Game.Train.Unit;
using MessagePack;
using Microsoft.Extensions.DependencyInjection;
using Server.Util.MessagePack;
using UnityEngine;

namespace Server.Protocol.PacketResponse
{
    // 列車1編成の運行設定（時刻表の丸ごと置換・自動運転ON/OFF）を受ける
    // Accepts per-train operation settings (whole-timetable replacement and auto-run toggle)
    public class TrainScheduleEditProtocol : IPacketResponse
    {
        public const string ProtocolTag = "va:trainScheduleEdit";

        private readonly ITrainUnitLookupDatastore _trainUnitLookupDatastore;
        private readonly ITrainUnitSnapshotNotifyEvent _snapshotNotifyEvent;

        public TrainScheduleEditProtocol(ServiceProvider serviceProvider)
        {
            _trainUnitLookupDatastore = serviceProvider.GetService<ITrainUnitLookupDatastore>();
            _snapshotNotifyEvent = serviceProvider.GetService<ITrainUnitSnapshotNotifyEvent>();
        }

        public ProtocolMessagePackBase GetResponse(byte[] payload, PacketResponseContext context)
        {
            var request = MessagePackSerializer.Deserialize<TrainScheduleEditRequest>(payload);
            if (!_trainUnitLookupDatastore.TryGetTrainUnit(request.TrainUnitInstanceId, out var train))
            {
                return Reject(request, TrainScheduleEditFailureReason.TrainNotFound, $"train={request.TrainUnitInstanceId}");
            }

            switch (request.Operation)
            {
                case TrainScheduleEditOperation.ReplaceTimetable:
                    return ReplaceTimetable(request, train);
                case TrainScheduleEditOperation.SetAutoRun:
                    return SetAutoRun(request, train);
                default:
                    return Reject(request, TrainScheduleEditFailureReason.InvalidRequest, "unknown operation");
            }

            #region Internal

            ProtocolMessagePackBase ReplaceTimetable(TrainScheduleEditRequest data, TrainUnit trainUnit)
            {
                // 全駅を先に解決し、1つでも駅でなければ全体を拒否する
                // Resolve every station first; reject the whole request if any entry is not a station
                if (data.StationPositions == null || data.StationPositions.Contains(null))
                {
                    return Reject(data, TrainScheduleEditFailureReason.InvalidRequest, "station positions are missing");
                }

                var nodes = new List<IRailNode>(data.StationPositions.Count);
                foreach (var position in data.StationPositions)
                {
                    var block = ServerContext.WorldBlockDatastore.GetBlock(position.Vector3Int);
                    if (block == null)
                    {
                        return Reject(data, TrainScheduleEditFailureReason.StationBlockNotFound, $"pos={position.Vector3Int}");
                    }
                    if (!TrainTimetableStationNodeResolver.TryResolve(block, out var node))
                    {
                        return Reject(data, TrainScheduleEditFailureReason.NotTrainStation, $"pos={position.Vector3Int} type={block.BlockMasterElement.BlockType}");
                    }
                    nodes.Add(node);
                }

                // 適用直後に通知し、次tickの重複通知を消費する
                // Notify immediately after applying and consume the pending tick notification
                trainUnit.ReplaceTimetable(nodes);
                trainUnit.trainDiagram.ConsumeCurrentEntryChanged();
                _snapshotNotifyEvent.NotifySnapshot(trainUnit);
                return new TrainScheduleEditResponse(true, TrainScheduleEditFailureReason.None, data.Operation);
            }

            ProtocolMessagePackBase SetAutoRun(TrainScheduleEditRequest data, TrainUnit trainUnit)
            {
                // 空の時刻表を含む検証は既存の自動運転処理へ委ねる
                // Delegate validation, including empty timetables, to existing auto-run logic
                if (data.AutoRunEnabled)
                {
                    trainUnit.TurnOnAutoRun();
                }
                else
                {
                    trainUnit.TurnOffAutoRun();
                }
                _snapshotNotifyEvent.NotifySnapshot(trainUnit);
                return new TrainScheduleEditResponse(true, TrainScheduleEditFailureReason.None, data.Operation);
            }

            ProtocolMessagePackBase Reject(TrainScheduleEditRequest data, TrainScheduleEditFailureReason reason, string detail)
            {
                // 拒否は無音にせず理由をログへ残す
                // Never reject silently; leave the reason in the log
                Debug.LogWarning($"[TrainScheduleEdit] rejected op={data.Operation} reason={reason} {detail}");
                return new TrainScheduleEditResponse(false, reason, data.Operation);
            }

            #endregion
        }

        #region MessagePack

        [MessagePackObject]
        public class TrainScheduleEditRequest : ProtocolMessagePackBase
        {
            [Key(2)] public TrainUnitInstanceId TrainUnitInstanceId { get; set; }
            [Key(3)] public TrainScheduleEditOperation Operation { get; set; }
            [Key(4)] public List<Vector3IntMessagePack> StationPositions { get; set; }
            [Key(5)] public bool AutoRunEnabled { get; set; }

            [Obsolete("デシリアライズ用のコンストラクタです。基本的に使用しないでください。")]
            public TrainScheduleEditRequest()
            {
                Tag = ProtocolTag;
            }

            // Operationごとに必要な項目が違うので生成はstatic factoryに限る
            // Construction goes through static factories because each operation needs different fields
            private TrainScheduleEditRequest(TrainUnitInstanceId trainUnitInstanceId, TrainScheduleEditOperation operation, List<Vector3IntMessagePack> stationPositions, bool autoRunEnabled)
            {
                Tag = ProtocolTag;
                TrainUnitInstanceId = trainUnitInstanceId;
                Operation = operation;
                StationPositions = stationPositions;
                AutoRunEnabled = autoRunEnabled;
            }

            public static TrainScheduleEditRequest CreateReplaceTimetableRequest(TrainUnitInstanceId trainUnitInstanceId, IReadOnlyList<Vector3Int> stationPositions)
            {
                var positions = new List<Vector3IntMessagePack>(stationPositions.Count);
                foreach (var position in stationPositions)
                {
                    positions.Add(new Vector3IntMessagePack(position));
                }
                return new TrainScheduleEditRequest(trainUnitInstanceId, TrainScheduleEditOperation.ReplaceTimetable, positions, false);
            }

            public static TrainScheduleEditRequest CreateSetAutoRunRequest(TrainUnitInstanceId trainUnitInstanceId, bool autoRunEnabled)
            {
                return new TrainScheduleEditRequest(trainUnitInstanceId, TrainScheduleEditOperation.SetAutoRun, new List<Vector3IntMessagePack>(), autoRunEnabled);
            }
        }

        [MessagePackObject]
        public class TrainScheduleEditResponse : ProtocolMessagePackBase
        {
            [Key(2)] public bool Success { get; set; }
            [Key(3)] public TrainScheduleEditFailureReason FailureReason { get; set; }
            [Key(4)] public TrainScheduleEditOperation Operation { get; set; }

            [Obsolete("デシリアライズ用のコンストラクタです。基本的に使用しないでください。")]
            public TrainScheduleEditResponse()
            {
                Tag = ProtocolTag;
            }

            public TrainScheduleEditResponse(bool success, TrainScheduleEditFailureReason failureReason, TrainScheduleEditOperation operation)
            {
                Tag = ProtocolTag;
                Success = success;
                FailureReason = failureReason;
                Operation = operation;
            }
        }

        #endregion
    }
}
