using System;
using Game.Challenge;
using Game.Challenge.Task;
using MessagePack;
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
            var messagePack = new CompletedChallengeEventMessage(currentChallenge.Config.Id);
            var payload = MessagePackSerializer.Serialize(messagePack);
            
            var playerId = currentChallenge.PlayerId;
            _eventProtocolProvider.AddEvent(playerId, EventTag, payload);
        }
    }
    
    [MessagePackObject]
    public class CompletedChallengeEventMessage
    {
        [Key(0)] public int CompletedChallengeId { get; set; }
        
        [Obsolete("デシリアライズ用のコンストラクタです。基本的に使用しないでください。")]
        public CompletedChallengeEventMessage()
        {
        }
        
        public CompletedChallengeEventMessage(int completedChallengeId)
        {
            CompletedChallengeId = completedChallengeId;
        }
    }
}