using System;
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
        
        private void OnCompletedChallenge(IChallengeTask currentChallenge)
        {
            var messagePack = new CompletedChallengeEventMessage(currentChallenge.ChallengeMasterElement.ChallengeGuid);
            var payload = MessagePackSerializer.Serialize(messagePack);
            
            var playerId = currentChallenge.PlayerId;
            _eventProtocolProvider.AddEvent(playerId, EventTag, payload);
        }
    }
    
    [MessagePackObject]
    public class CompletedChallengeEventMessage
    {
        [Key(0)] public string CompletedChallengeGuidStr { get; set; }
        [JsonIgnore] public Guid CompletedChallengeGuid => Guid.Parse(CompletedChallengeGuidStr);
        
        [Obsolete("デシリアライズ用のコンストラクタです。基本的に使用しないでください。")]
        public CompletedChallengeEventMessage()
        {
        }
        
        public CompletedChallengeEventMessage(Guid completedChallengeGuid)
        {
            CompletedChallengeGuidStr = completedChallengeGuid.ToString();
        }
    }
}