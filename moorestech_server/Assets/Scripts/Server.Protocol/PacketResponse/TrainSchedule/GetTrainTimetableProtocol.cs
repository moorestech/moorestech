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
                return new GetTrainTimetableResponse(null);
            }
            return new GetTrainTimetableResponse(new TrainTimetableMessagePack(TrainTimetableSnapshotFactory.Create(train)));
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
            // Timetable が null なら対象列車は存在しない。bool Found との二重表現を避け、
            // 「見つからない」は Timetable 全体の null 一本で表す（GetGearNetworkInfoProtocol と同型）
            // Timetable == null means the train does not exist. Avoids a double representation with a bool Found;
            // "not found" is expressed solely by a whole-object null on Timetable (mirrors GetGearNetworkInfoProtocol)
            [Key(2)] public TrainTimetableMessagePack Timetable { get; set; }

            [Obsolete("デシリアライズ用のコンストラクタです。基本的に使用しないでください。")]
            public GetTrainTimetableResponse() { Tag = ProtocolTag; }

            public GetTrainTimetableResponse(TrainTimetableMessagePack timetable)
            {
                Tag = ProtocolTag;
                Timetable = timetable;
            }
        }
    }
}
