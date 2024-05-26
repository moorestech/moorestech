using System;
using Game.Challenge.Task;
using MessagePack;

namespace Server.Event.EventReceive
{
    public class CompletedChallengeEventPacket
    {
        public const string EventTag = "va:event:completedChallenge";

        private readonly EventProtocolProvider _eventProtocolProvider;

        public CompletedChallengeEventPacket(EventProtocolProvider eventProtocolProvider)
        {
            _eventProtocolProvider = eventProtocolProvider;
            throw new NotImplementedException("チャレンジのサブスクライブ");
        }

        private void OnCompletedChallenge(CurrentChallenge currentChallenge)
        {
            var messagePack = new CompletedChallenge(currentChallenge.Config.Id);
            var payload = MessagePackSerializer.Serialize(messagePack);

            var playerId = currentChallenge.PlayerId;
            _eventProtocolProvider.AddEvent(playerId, EventTag, payload);
        }
    }

    [MessagePackObject]
    public class CompletedChallenge
    {
        [Obsolete("デシリアライズ用のコンストラクタです。基本的に使用しないでください。")]
        public CompletedChallenge() { }

        [Key(0)]
        public int CompletedChallengeId { get; set; }

        public CompletedChallenge(int completedChallengeId)
        {
            CompletedChallengeId = completedChallengeId;
        }
    }
}