using Client.Localization;
using Client.WebUiHost.Boot;
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
            var gate = new EventLanguageGate();
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
            var gate = new EventLanguageGate();
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
    }
}
