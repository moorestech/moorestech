using System;
using System.Collections.Generic;
using MessagePack;

namespace Server.Protocol.PacketResponse
{
    public class RailConnectWithPlacePierProtocol : IPacketResponse
    {
        public const string Tag = "va:railConnectWithPlacePier";
        
        public ProtocolMessagePackBase GetResponse(List<byte> payload)
        {
            var request = MessagePackSerializer.Deserialize<RailConnectWithPlacePierRequest>(payload.ToArray());
            
            return null;
        }
        
        [MessagePackObject]
        public class RailConnectWithPlacePierRequest : ProtocolMessagePackBase
        {
            [Key(2)] public int FromNodeId { get; set; }
            [Key(3)] public Guid FromGuid { get; set; }
            [Key(4)] public PlaceBlockFromHotBarProtocol.PlaceInfoMessagePack PierPlaceInfo
            {
                get;
                set;
            }
            [Key(5)] public int PlayerId { get; set; }
            [Key(6)] public int PierInventorySlot { get; set; }
            [Key(7)] public Guid RailTypeGuid { get; set; }
            
            [Obsolete("デシリアライズ用のコンストラクタです。基本的に使用しないでください。")]
            public RailConnectWithPlacePierRequest()
            {
                Tag = RailConnectWithPlacePierProtocol.Tag;
            }
            
            public static RailConnectWithPlacePierRequest Create(int playerId, int fromNodeId, Guid fromGuid, int pierInventorySlot, PlaceInfo placeInfo, Guid railTypeGuid)
            {
                return new RailConnectWithPlacePierRequest
                {
                    PlayerId = playerId,
                    FromNodeId = fromNodeId,
                    FromGuid = fromGuid,
                    PierInventorySlot = pierInventorySlot,
                    RailTypeGuid = railTypeGuid,
                    PierPlaceInfo = new PlaceBlockFromHotBarProtocol.PlaceInfoMessagePack(placeInfo),
                };
            }
        }
    }
}