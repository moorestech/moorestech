using System;
using System.Collections.Generic;
using System.Linq;
using Game.Challenge;
using Game.UnlockState;
using MessagePack;
using Microsoft.Extensions.DependencyInjection;
using Server.Event.EventReceive;

namespace Server.Protocol.PacketResponse
{
    public class GetChallengeInfoProtocol : IPacketResponse
    {
        public const string ProtocolTag = "va:getChallengeInfo";
        
        private readonly ChallengeDatastore _challengeDatastore;
        private readonly IGameUnlockStateDataController _gameUnlockStateDataController;
        
        public GetChallengeInfoProtocol(ServiceProvider serviceProvider)
        {
            _challengeDatastore = serviceProvider.GetService<ChallengeDatastore>();
            _gameUnlockStateDataController = serviceProvider.GetService<IGameUnlockStateDataController>();
        }
        
        public ProtocolMessagePackBase GetResponse(byte[] payload)
        {
            var challengeCategories = CompletedChallengeEventPacket.GetChallengeCategories(_challengeDatastore, _gameUnlockStateDataController);
            
            return new ResponseChallengeInfoMessagePack(challengeCategories);
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
            public ResponseChallengeInfoMessagePack(List<ChallengeCategoryMessagePack> categories)
            {
                Tag = ProtocolTag;
                Categories = categories;
            }
        }
    }
}