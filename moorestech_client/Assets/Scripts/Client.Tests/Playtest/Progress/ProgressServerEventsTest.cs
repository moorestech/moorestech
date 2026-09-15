using System;
using Client.Game.InGame.Playtest.Progress;
using Client.Game.InGame.Playtest.Progress.Record;
using Client.Game.InGame.Playtest.Progress.Record.Events;
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

            var progressEvent = ProgressServerEvents.ResearchCompleted(payload, Now, 42);

            Assert.AreEqual("researchCompleted", (string)progressEvent.ToJson()["type"]);
            Assert.AreEqual(researchGuid.ToString(), progressEvent.ResearchGuid);
            Assert.AreEqual(42ul, progressEvent.Tick);
            Assert.AreEqual(ProgressUtcTime.ToIso(Now), progressEvent.T);
        }

        [Test]
        public void チャレンジ完了のpayloadがchallengeCompletedの1件になる()
        {
            var challengeGuid = Guid.NewGuid();
#pragma warning disable CS0618
            var message = new CompletedChallengeEventMessagePack { CompletedChallengeGuidStr = challengeGuid.ToString() };
#pragma warning restore CS0618
            var payload = MessagePackSerializer.Serialize(message);

            var progressEvent = ProgressServerEvents.ChallengeCompleted(payload, Now, 7);

            Assert.AreEqual("challengeCompleted", (string)progressEvent.ToJson()["type"]);
            Assert.AreEqual(challengeGuid.ToString(), progressEvent.ChallengeGuid);
        }

        [Test]
        public void クラフト成立のpayloadがレシピ付きのcraftCompletedの1件になる()
        {
            var recipeGuid = Guid.NewGuid();
            var payload = MessagePackSerializer.Serialize(new CraftCompletedEventPacket.CraftCompletedEventMessagePack(1, recipeGuid));

            var json = ProgressServerEvents.CraftCompleted(payload, Now, 9).ToJson();

            Assert.AreEqual("craftCompleted", (string)json["type"]);
            Assert.AreEqual(recipeGuid.ToString(), (string)json["data"]["recipeGuid"]);
            Assert.AreEqual(9ul, (ulong)json["tick"]);
        }

        // 書いた行は種別ごとのクラスへそのまま戻る。未知の種別は例外にせず捨てる
        // A written line comes back as its own type's class unchanged; an unknown type is dropped rather than thrown
        [Test]
        public void 書いた行は同じ種別へ読み戻せ未知の種別は捨てる()
        {
            var line = ProgressEventLine.ToJsonLine(new CraftCompletedEvent(Now, 3, "recipe-1"));

            var restored = ProgressEventLine.FromJsonLine(line) as CraftCompletedEvent;
            Assert.IsNotNull(restored, "書いた種別のクラスへ戻っていない");
            Assert.AreEqual("recipe-1", restored.RecipeGuid);

            UnityEngine.TestTools.LogAssert.Expect(UnityEngine.LogType.Warning, new System.Text.RegularExpressions.Regex("未知のイベント種別"));
            Assert.IsNull(ProgressEventLine.FromJsonLine("{\"t\":\"2026-09-14T01:02:03Z\",\"tick\":1,\"type\":\"nope\",\"data\":{}}"));
        }
    }
}
