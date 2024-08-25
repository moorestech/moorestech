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
        public const string Tag = "va:getChallengeInfo";
        
        private readonly ChallengeDatastore _challengeDatastore;
        
        public GetChallengeInfoProtocol(ServiceProvider serviceProvider)
        {
            _challengeDatastore = serviceProvider.GetService<ChallengeDatastore>();
        }
        
        public ProtocolMessagePackBase GetResponse(List<byte> payload)
        {
            var data = MessagePackSerializer.Deserialize<RequestChallengeMessagePack>(payload.ToArray());
            
            var info = _challengeDatastore.GetOrCreateChallengeInfo(data.PlayerId);
            var currentChallengeIds = info.CurrentChallenges.Select(c => c.ChallengeElement.Id).ToList();
            
            return new ResponseChallengeInfoMessagePack(data.PlayerId, currentChallengeIds, info.CompletedChallengeGuids);
        }
    }
    
    [MessagePackObject]
    public class RequestChallengeMessagePack : ProtocolMessagePackBase
    {
        [Obsolete("デシリアライズ用のコンストラクタです。基本的に使用しないでください。")]
        public RequestChallengeMessagePack()
        {
        }
        
        public RequestChallengeMessagePack(int playerId)
        {
            Tag = GetChallengeInfoProtocol.Tag;
            PlayerId = playerId;
        }
        
        [Key(2)] public int PlayerId { get; set; }
    }
    
    [MessagePackObject]
    public class ResponseChallengeInfoMessagePack : ProtocolMessagePackBase
    {
        [Key(2)] public int PlayerId { get; set; }
        
        [Key(3)] public List<int> CurrentChallengeIds { get; set; }
        
        [Key(4)] public List<int> CompletedChallengeIds { get; set; }
        
        [Obsolete("デシリアライズ用のコンストラクタです。基本的に使用しないでください。")]
        public ResponseChallengeInfoMessagePack()
        {
        }
        
        public ResponseChallengeInfoMessagePack(int playerId, List<int> currentChallengeIds, List<int> completedChallengeIds)
        {
            Tag = GetChallengeInfoProtocol.Tag;
            PlayerId = playerId;
            CurrentChallengeIds = currentChallengeIds;
            CompletedChallengeIds = completedChallengeIds;
        }
    }
}