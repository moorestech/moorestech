using System;
using Game.Train.Unit;
using MessagePack;
using Microsoft.Extensions.DependencyInjection;
using Server.Util.MessagePack;
using UnityEngine;

namespace Server.Protocol.PacketResponse
{
    // 時刻表タブを開いたときの初期データを返す
    // Return the initial data when the timetable tab opens
    public class GetTrainTimetableProtocol : IPacketResponse
    {
        public const string ProtocolTag = "va:getTrainTimetable";
        private readonly ITrainUnitLookupDatastore _trainUnitLookupDatastore;

        public GetTrainTimetableProtocol(ServiceProvider serviceProvider)
        {
            _trainUnitLookupDatastore = serviceProvider.GetService<ITrainUnitLookupDatastore>();
        }

        public ProtocolMessagePackBase GetResponse(byte[] payload, PacketResponseContext context)
        {
            var request = MessagePackSerializer.Deserialize<GetTrainTimetableRequest>(payload);
            if (!_trainUnitLookupDatastore.TryGetTrainUnit(request.TrainUnitInstanceId, out var train))
            {
                Debug.LogWarning($"[GetTrainTimetable] train not found: {request.TrainUnitInstanceId}");
                return new GetTrainTimetableResponse(false, null);
            }
            return new GetTrainTimetableResponse(true, new TrainTimetableMessagePack(TrainTimetableSnapshotFactory.Create(train)));
        }

        [MessagePackObject]
        public class GetTrainTimetableRequest : ProtocolMessagePackBase
        {
            [Key(2)] public TrainUnitInstanceId TrainUnitInstanceId { get; set; }

            [Obsolete("デシリアライズ用のコンストラクタです。基本的に使用しないでください。")]
            public GetTrainTimetableRequest() { Tag = ProtocolTag; }

            public GetTrainTimetableRequest(TrainUnitInstanceId trainUnitInstanceId)
            {
                Tag = ProtocolTag;
                TrainUnitInstanceId = trainUnitInstanceId;
            }
        }

        [MessagePackObject]
        public class GetTrainTimetableResponse : ProtocolMessagePackBase
        {
            [Key(2)] public bool Found { get; set; }
            [Key(3)] public TrainTimetableMessagePack Timetable { get; set; }

            [Obsolete("デシリアライズ用のコンストラクタです。基本的に使用しないでください。")]
            public GetTrainTimetableResponse() { Tag = ProtocolTag; }

            public GetTrainTimetableResponse(bool found, TrainTimetableMessagePack timetable)
            {
                Tag = ProtocolTag;
                Found = found;
                Timetable = timetable;
            }
        }
    }
}
