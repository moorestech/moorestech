using System.Collections.Concurrent;
using System.Reflection;
using Client.Localization;
using Client.WebUiHost.Boot;
using Client.WebUiHost.Game.Actions;
using Client.WebUiHost.Game.Actions.EventMode;
using Client.WebUiHost.Game.EventMode;
using Client.WebUiHost.Game.Topics.EventMode;
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

        [Test]
        public void Snapshotは待機状態をwaitingとして配る()
        {
            var gate = new EventLanguageGate(true);
            var topic = new EventLanguageGateTopic(new WebSocketHub(), gate);

            var waitingJson = JObject.Parse(topic.GetSnapshotJsonAsync().GetAwaiter().GetResult());
            Assert.IsTrue(waitingJson["waiting"].Value<bool>());

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
            hub.RegisterTopic(EventLanguageGateTopic.TopicName, new EventLanguageGateTopic(hub, gate));

            var revisionBefore = GetTopicRevision(hub, EventLanguageGateTopic.TopicName);
            gate.TrySelectLanguage("english");
            var revisionAfter = GetTopicRevision(hub, EventLanguageGateTopic.TopicName);

            Assert.Greater(revisionAfter, revisionBefore);
        }

        [Test]
        public void Binderはtopicとactionをhubへ登録する()
        {
            var hub = new WebSocketHub();

            EventLanguageGateBinder.Bind(hub, true);

            var topicHandlers = GetPrivateField<ConcurrentDictionary<string, ITopicHandler>>(hub, "_handlers");
            Assert.IsTrue(topicHandlers.ContainsKey(EventLanguageGateTopic.TopicName));

            var actionHandlers = GetPrivateField<ConcurrentDictionary<string, IActionHandler>>(hub, "_actionHandlers");
            Assert.IsTrue(actionHandlers.ContainsKey("event_mode.select_language"));
        }

        private static long GetTopicRevision(WebSocketHub hub, string topic)
        {
            var revisions = GetPrivateField<ConcurrentDictionary<string, long>>(hub, "_topicRevisions");
            revisions.TryGetValue(topic, out var revision);
            return revision;
        }

        // 公開動作を駆動する実レジストリを直接読み、配線漏れをテストで検出できるようにする
        // Read the real registry that drives public behavior directly so wiring gaps are caught by tests
        private static T GetPrivateField<T>(object instance, string fieldName)
        {
            var field = instance.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(field);
            return (T)field.GetValue(instance);
        }
    }
}
