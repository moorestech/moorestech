using System;
using System.Collections.Generic;
using Game.Challenge;
using Game.Challenge.Task;
using MessagePack;
using Newtonsoft.Json;
using UniRx;

namespace Server.Event.EventReceive
{
    public class CompletedChallengeEventPacket
    {
        public const string EventTag = "va:event:completedChallenge";
        
        private readonly EventProtocolProvider _eventProtocolProvider;
        
        public CompletedChallengeEventPacket(EventProtocolProvider eventProtocolProvider, ChallengeEvent challengeEvent)
        {
            _eventProtocolProvider = eventProtocolProvider;
            challengeEvent.OnCompleteChallenge.Subscribe(OnCompletedChallenge);
        }
        
        private void OnCompletedChallenge(ChallengeEvent.CompleteChallengeEventProperty completeProperty)
        {
            var messagePack = new CompletedChallengeEventMessagePack(completeProperty);
            var payload = MessagePackSerializer.Serialize(messagePack);
            
            var playerId = completeProperty.ChallengeTask.PlayerId;
            _eventProtocolProvider.AddEvent(playerId, EventTag, payload);
        }
    }
    
    [MessagePackObject]
    public class CompletedChallengeEventMessagePack
    {
        [Key(0)] public string CompletedChallengeGuidStr { get; set; }
        [Key(1)] public List<string> NextChallengeGuidsStr { get; set; }
        
        [IgnoreMember] public Guid CompletedChallengeGuid => Guid.Parse(CompletedChallengeGuidStr);
        [IgnoreMember] public List<Guid> NextChallengeGuids => NextChallengeGuidsStr.ConvertAll(Guid.Parse);
        
        [Obsolete("デシリアライズ用のコンストラクタです。基本的に使用しないでください。")]
        public CompletedChallengeEventMessagePack()
        {
        }
        
        public CompletedChallengeEventMessagePack(ChallengeEvent.CompleteChallengeEventProperty completeProperty)
        {
            CompletedChallengeGuidStr = completeProperty.ChallengeTask.ChallengeMasterElement.ChallengeGuid.ToString();
            NextChallengeGuidsStr = completeProperty.NextChallengeMasterElements.ConvertAll(e => e.ChallengeGuid.ToString());
        }
    }
}