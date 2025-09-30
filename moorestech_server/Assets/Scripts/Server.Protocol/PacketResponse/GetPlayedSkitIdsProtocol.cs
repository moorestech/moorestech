using System;
using System.Collections.Generic;
using Game.Challenge;
using MessagePack;
using Microsoft.Extensions.DependencyInjection;

namespace Server.Protocol.PacketResponse
{
    public class GetPlayedSkitIdsProtocol : IPacketResponse
    {
        public const string ProtocolTag = "va:getPlayedSkitIds";
        private readonly ChallengeDatastore _challengeDatastore;
        
        public GetPlayedSkitIdsProtocol(ServiceProvider serviceProvider)
        {
            _challengeDatastore = serviceProvider.GetService<ChallengeDatastore>();
        }
        
        public ProtocolMessagePackBase GetResponse(List<byte> payload)
        {
            return new ResponseGetPlayedSkitIdsMessagePack(_challengeDatastore.CurrentChallengeInfo.PlayedSkitIds);
        }
        
        [MessagePackObject]
        public class RequestGetPlayedSkitIdsMessagePack : ProtocolMessagePackBase
        {
            public RequestGetPlayedSkitIdsMessagePack()
            {
                Tag = ProtocolTag;
            }
        }
        
        [MessagePackObject]
        public class ResponseGetPlayedSkitIdsMessagePack : ProtocolMessagePackBase
        {
            [Key(2)] public List<string> PlayedSkitIds;
            
            [Obsolete("デシリアライズ用のコンストラクタです。基本的に使用しないでください。")]
            public ResponseGetPlayedSkitIdsMessagePack() {}
            
            public ResponseGetPlayedSkitIdsMessagePack(List<string> playedSkitIds)
            {
                Tag = ProtocolTag;
                PlayedSkitIds = playedSkitIds;
            }
        }
    }
}