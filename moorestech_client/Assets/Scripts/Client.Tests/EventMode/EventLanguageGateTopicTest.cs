using Client.Localization;
using Client.Tests.WebUi.Gate;
using Client.WebUiHost.Boot;
using Client.WebUiHost.Game.Actions.EventMode;
using Client.WebUiHost.Game.EventMode;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace Client.Tests.EventMode
{
    public class EventLanguageGateTopicTest
    {
        [SetUp]
        public void SetUp()
        {
            Localize.Initialize();
        }

        // 開始を止めるゲートは言語選択1枚なので、配るのは waiting だけで順序は持たない
        // The language gate is the only one holding the start, so only waiting travels and no order comes with it
        [Test]
        public void Snapshotは待機状態をwaitingとして配り順序は持たない()
        {
            var gate = new EventLanguageGate(true);
            var topic = new EventLanguageGateTopic(new WebSocketHub(), gate);

            var waitingJson = JObject.Parse(topic.GetSnapshotJsonAsync().GetAwaiter().GetResult());
            Assert.IsTrue(waitingJson["waiting"].Value<bool>());
            Assert.IsNull(waitingJson["precedence"]);

            gate.TrySelectLanguage("english");
            var selectedJson = JObject.Parse(topic.GetSnapshotJsonAsync().GetAwaiter().GetResult());
            Assert.IsFalse(selectedJson["waiting"].Value<bool>());
        }

        [Test]
        public void 選択アクションはゲートを開き未知localeは失敗を返す()
        {
            var gate = new EventLanguageGate(true);
            var handler = new SelectEventLanguageActionHandler(gate);

            Assert.AreEqual("event_mode.select_language", handler.ActionType);

            var failed = handler.ExecuteAsync(JObject.Parse("{\"locale\":\"klingon\"}")).GetAwaiter().GetResult();
            Assert.IsFalse(failed.Ok);
            Assert.AreEqual("unknown_locale", failed.Error);
            Assert.IsTrue(gate.IsWaitingSelection);

            var succeeded = handler.ExecuteAsync(JObject.Parse("{\"locale\":\"japanese\"}")).GetAwaiter().GetResult();
            Assert.IsTrue(succeeded.Ok);
            Assert.IsFalse(gate.IsWaitingSelection);
        }

        [Test]
        public void 選択で待機変化がhubのtopicRevisionへ反映される()
        {
            var hub = new WebSocketHub();
            var gate = new EventLanguageGate(true);
            EventLanguageGateTopic.Register(hub, gate);

            var revisionBefore = hub.GetTopicRevision(EventLanguageGateTopic.TopicName);
            gate.TrySelectLanguage("english");
            var revisionAfter = hub.GetTopicRevision(EventLanguageGateTopic.TopicName);

            Assert.Greater(revisionAfter, revisionBefore);
        }

        [Test]
        public void Binderはtopicとactionをhubへ登録する()
        {
            var hub = new WebSocketHub();

            EventLanguageGateBinder.Bind(hub, true);

            Assert.IsNotNull(hub.ResolveTopic(EventLanguageGateTopic.TopicName));
            Assert.IsNotNull(hub.ResolveAction("event_mode.select_language"));
        }

        [Test]
        public void 待機しないBindでもtopicを登録しwaitingをfalseで配る()
        {
            var hub = new WebSocketHub();

            EventLanguageGateBinder.Bind(hub, false);

            StartGateTopicAssert.AssertWaiting(hub, EventLanguageGateTopic.TopicName, false);
            Assert.IsNotNull(hub.ResolveAction("event_mode.select_language"));
        }
    }
}
