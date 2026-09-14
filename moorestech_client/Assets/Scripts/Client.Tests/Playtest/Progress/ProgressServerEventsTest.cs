using System;
using Client.Game.InGame.Playtest.Progress;
using MessagePack;
using NUnit.Framework;
using Server.Event.EventReceive;

namespace Client.Tests.Playtest
{
    // サーバーイベントのpayloadから進行記録の1行になるまでを、購読を張らずに固定する
    // Pins the mapping from a server event payload to one progress line without attaching any subscription
    public class ProgressServerEventsTest
    {
        private static readonly DateTime Now = new(2026, 9, 14, 1, 2, 3, DateTimeKind.Utc);

        [Test]
        public void 研究完了のpayloadがresearchCompletedの1件になる()
        {
            var researchGuid = Guid.NewGuid();
            var payload = MessagePackSerializer.Serialize(new ResearchCompleteEventPacket.ResearchCompleteEventMessagePack(1, researchGuid));

            var entry = ProgressServerEvents.ResearchCompleted(payload, Now, 42);

            Assert.AreEqual(ProgressEventType.ResearchCompleted, entry.Type);
            Assert.AreEqual(researchGuid.ToString(), ProgressEvents.ReadResearchGuid(entry));
            Assert.AreEqual(42ul, entry.Tick);
            Assert.AreEqual(ProgressUtcTime.ToIso(Now), entry.T);
        }

        [Test]
        public void チャレンジ完了のpayloadがchallengeCompletedの1件になる()
        {
            var challengeGuid = Guid.NewGuid();
#pragma warning disable CS0618
            var message = new CompletedChallengeEventMessagePack { CompletedChallengeGuidStr = challengeGuid.ToString() };
#pragma warning restore CS0618
            var payload = MessagePackSerializer.Serialize(message);

            var entry = ProgressServerEvents.ChallengeCompleted(payload, Now, 7);

            Assert.AreEqual(ProgressEventType.ChallengeCompleted, entry.Type);
            Assert.AreEqual(challengeGuid.ToString(), ProgressEvents.ReadChallengeGuid(entry));
        }
    }
}
