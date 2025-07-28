using System;
using System.Collections.Generic;
using System.Linq;
using Game.Challenge;
using MessagePack;
using Microsoft.Extensions.DependencyInjection;

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
            
            var info = _challengeDatastore.CurrentChallengeInfo;
            var currentChallengeIds = info.CurrentChallenges.Select(c => c.ChallengeMasterElement.ChallengeGuid).ToList();
            
            return new ResponseChallengeInfoMessagePack(currentChallengeIds, info.CompletedChallengeGuids);
        }
        
        
        [MessagePackObject]
        public class RequestChallengeMessagePack : ProtocolMessagePackBase
        {
            public RequestChallengeMessagePack()
            {
                Tag = ProtocolTag;
            }
        }
        
        [MessagePackObject]
        public class ResponseChallengeInfoMessagePack : ProtocolMessagePackBase
        {
            [Key(3)] public List<string> CurrentChallengeGuidsStr { get; set; }
            [Key(4)] public List<string> CompletedChallengeGuidsStr { get; set; }
            
            [IgnoreMember] public List<Guid> CurrentChallengeGuids => CurrentChallengeGuidsStr.Select(Guid.Parse).ToList();
            [IgnoreMember] public List<Guid> CompletedChallengeGuids => CompletedChallengeGuidsStr.Select(Guid.Parse).ToList();
            
            [Obsolete("デシリアライズ用のコンストラクタです。基本的に使用しないでください。")]
            public ResponseChallengeInfoMessagePack() { }
            public ResponseChallengeInfoMessagePack(List<Guid> currentChallengeIds, List<Guid> completedChallengeIds)
            {
                Tag = ProtocolTag;
                CurrentChallengeGuidsStr = currentChallengeIds.Select(x => x.ToString()).ToList();
                CompletedChallengeGuidsStr = completedChallengeIds.Select(x => x.ToString()).ToList();
            }
        }
    }
}