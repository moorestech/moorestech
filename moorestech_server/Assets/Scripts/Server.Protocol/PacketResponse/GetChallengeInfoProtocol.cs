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

            var info = _challengeDatastore.GetChallengeInfo(data.PlayerId);
            var currentChallengeIds = info.CurrentChallenges.Select(c => c.Config.Id).ToList();

            return new ChallengeInfoMessagePack(data.PlayerId, currentChallengeIds, info.CompletedChallengeIds);
        }
    }

    [MessagePackObject]
    public class RequestChallengeMessagePack : ProtocolMessagePackBase
    {
        [Key(0)]
        public int PlayerId { get; set; }

        [Obsolete("デシリアライズ用のコンストラクタです。基本的に使用しないでください。")]
        public RequestChallengeMessagePack() { }

        public RequestChallengeMessagePack(int playerId)
        {
            Tag = GetChallengeInfoProtocol.Tag;
            PlayerId = playerId;
        }
    }

    [MessagePackObject]
    public class ChallengeInfoMessagePack : ProtocolMessagePackBase
    {
        [Key(0)]
        public int PlayerId { get; set; }

        [Key(1)]
        public List<int> CurrentChallengeIds { get; set; }

        [Key(2)]
        public List<int> CompletedChallengeIds { get; set; }

        [Obsolete("デシリアライズ用のコンストラクタです。基本的に使用しないでください。")]
        public ChallengeInfoMessagePack() { }

        public ChallengeInfoMessagePack(int playerId, List<int> currentChallengeIds, List<int> completedChallengeIds)
        {
            Tag = GetChallengeInfoProtocol.Tag;
            PlayerId = playerId;
            CurrentChallengeIds = currentChallengeIds;
            CompletedChallengeIds = completedChallengeIds;
        }
    }
}