using System;
using System.Collections.Generic;
using Game.Train.Event;
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
                    // 既定値0(未指定)と未定義値はここで止める
                    // The default 0 (unspecified) and undefined values stop here
                    return Reject(request, TrainScheduleEditFailureReason.InvalidRequest, $"unknown operation={(int)request.Operation}");
            }

            #region Internal

            ProtocolMessagePackBase ReplaceTimetable(TrainScheduleEditRequest data, TrainUnit trainUnit)
            {
                // 全駅を先に解決し、1つでも駅でなければ全体を拒否する
                // Resolve every station first; reject the whole request if any entry is not a station
                if (!TrainScheduleStopResolver.TryResolve(data.Stops, out var stops, out var failureReason, out var detail))
                {
                    return Reject(data, failureReason, detail);
                }

                // 適用そのものが時刻表イベントを押し出すので、ここでは走行同期だけ送る
                // Applying pushes the timetable event by itself, so only the motion sync is sent here
                trainUnit.ReplaceTimetable(stops);
                _snapshotNotifyEvent.NotifySnapshot(trainUnit);
                return new TrainScheduleEditResponse(true, TrainScheduleEditFailureReason.None, data.Operation, trainUnit.IsAutoRun);
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
                // 実状態が変わったときだけ時刻表イベントが出る。応答のappliedIsAutoRunが最終的な正
                // The timetable event fires only on a real change; the response's appliedIsAutoRun is authoritative
                _snapshotNotifyEvent.NotifySnapshot(trainUnit);
                return new TrainScheduleEditResponse(true, TrainScheduleEditFailureReason.None, data.Operation, trainUnit.IsAutoRun);
            }

            ProtocolMessagePackBase Reject(TrainScheduleEditRequest data, TrainScheduleEditFailureReason reason, string detail)
            {
                // 拒否は無音にせず理由をログへ残す
                // Never reject silently; leave the reason in the log
                Debug.LogWarning($"[TrainScheduleEdit] rejected op={data.Operation} reason={reason} {detail}");
                // 列車が取れない拒否では適用後の実状態も無いのでfalseを載せる
                // A rejection without a train has no applied state, so false is sent
                return new TrainScheduleEditResponse(false, reason, data.Operation, false);
            }

            #endregion
        }

        #region MessagePack

        [MessagePackObject]
        public class TrainScheduleEditRequest : ProtocolMessagePackBase
        {
            [Key(2)] public TrainUnitInstanceId TrainUnitInstanceId { get; set; }
            [Key(3)] public TrainScheduleEditOperation Operation { get; set; }
            [Key(4)] public List<TrainTimetableStopMessagePack> Stops { get; set; }
            [Key(5)] public bool AutoRunEnabled { get; set; }

            [Obsolete("デシリアライズ用のコンストラクタです。基本的に使用しないでください。")]
            public TrainScheduleEditRequest()
            {
                Tag = ProtocolTag;
            }

            // Operationごとに必要な項目が違うので生成はstatic factoryに限る
            // Construction goes through static factories because each operation needs different fields
            private TrainScheduleEditRequest(TrainUnitInstanceId trainUnitInstanceId, TrainScheduleEditOperation operation, List<TrainTimetableStopMessagePack> stops, bool autoRunEnabled)
            {
                Tag = ProtocolTag;
                TrainUnitInstanceId = trainUnitInstanceId;
                Operation = operation;
                Stops = stops;
                AutoRunEnabled = autoRunEnabled;
            }

            public static TrainScheduleEditRequest CreateReplaceTimetableRequest(TrainUnitInstanceId trainUnitInstanceId, IReadOnlyList<TrainTimetableStop> stops)
            {
                var messagePackStops = new List<TrainTimetableStopMessagePack>(stops.Count);
                foreach (var stop in stops)
                {
                    messagePackStops.Add(new TrainTimetableStopMessagePack(stop));
                }
                return new TrainScheduleEditRequest(trainUnitInstanceId, TrainScheduleEditOperation.ReplaceTimetable, messagePackStops, false);
            }

            public static TrainScheduleEditRequest CreateSetAutoRunRequest(TrainUnitInstanceId trainUnitInstanceId, bool autoRunEnabled)
            {
                return new TrainScheduleEditRequest(trainUnitInstanceId, TrainScheduleEditOperation.SetAutoRun, new List<TrainTimetableStopMessagePack>(), autoRunEnabled);
            }
        }

        [MessagePackObject]
        public class TrainScheduleEditResponse : ProtocolMessagePackBase
        {
            [Key(2)] public bool Success { get; set; }
            [Key(3)] public TrainScheduleEditFailureReason FailureReason { get; set; }
            [Key(4)] public TrainScheduleEditOperation Operation { get; set; }
            // 要求した状態に到達したかをUIが判定できるよう、適用後の実状態を載せる
            // Carries the state after applying so the UI can tell whether the request took effect
            [Key(5)] public bool AppliedIsAutoRun { get; set; }

            [Obsolete("デシリアライズ用のコンストラクタです。基本的に使用しないでください。")]
            public TrainScheduleEditResponse()
            {
                Tag = ProtocolTag;
            }

            public TrainScheduleEditResponse(bool success, TrainScheduleEditFailureReason failureReason, TrainScheduleEditOperation operation, bool appliedIsAutoRun)
            {
                Tag = ProtocolTag;
                Success = success;
                FailureReason = failureReason;
                Operation = operation;
                AppliedIsAutoRun = appliedIsAutoRun;
            }
        }

        #endregion
    }
}
