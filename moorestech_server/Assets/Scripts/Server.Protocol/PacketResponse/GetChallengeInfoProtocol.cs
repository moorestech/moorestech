using System;
using System.Collections.Generic;
using System.Linq;
using Game.Challenge;
using MessagePack;
using Microsoft.Extensions.DependencyInjection;
using Server.Event.EventReceive;

namespace Server.Protocol.PacketResponse
{
    public class GetChallengeInfoProtocol : IPacketResponse
    {
        public const string ProtocolTag = "va:getChallengeInfo";
        
        private readonly ChallengeDatastore _challengeDatastore;
        
        public GetChallengeInfoProtocol(ServiceProvider serviceProvider)
        {
            _challengeDatastore = serviceProvider.GetService<ChallengeDatastore>();
        }
        
        public ProtocolMessagePackBase GetResponse(List<byte> payload)
        {
            var data = MessagePackSerializer.Deserialize<RequestChallengeMessagePack>(payload.ToArray());
            
            
            return new ResponseChallengeInfoMessagePack(currentChallengeIds, info.CompletedChallenges);
        }
        
        
        [MessagePackObject]
        public class RequestChallengeMessagePack : ProtocolMessagePackBase
        {
            public RequestChallengeMessagePack() { Tag = ProtocolTag; }
        }
        
        [MessagePackObject]
        public class ResponseChallengeInfoMessagePack : ProtocolMessagePackBase
        {
            [Key(2)] public List<ChallengeCategoryMessagePack> Categories { get; set; }
            
            
            [Obsolete("デシリアライズ用のコンストラクタです。基本的に使用しないでください。")]
            public ResponseChallengeInfoMessagePack() { }
            public ResponseChallengeInfoMessagePack(List<Guid> currentChallengeIds, List<Guid> completedChallengeIds)
            {
                Tag = ProtocolTag;
            }
        }
    }
}