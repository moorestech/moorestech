using Client.WebUiHost.Boot;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace Client.Tests.WebUi.Gate
{
    // 開始ゲートtopicのsnapshotを待機状態と順番の両方で検証する。Web側はprecedence欠けのpayloadを拒否するため必ず対で見る
    // Asserts a start-gate topic snapshot on both waiting and precedence; the web rejects payloads lacking precedence, so both are always checked
    internal static class StartGateTopicAssert
    {
        internal static void AssertWaiting(WebSocketHub hub, string topicName, bool waiting, int precedence)
        {
            var topic = hub.ResolveTopic(topicName);
            Assert.IsNotNull(topic, $"{topicName} のtopicが登録されていない");
            var json = JObject.Parse(topic.GetSnapshotJsonAsync().GetAwaiter().GetResult());
            Assert.AreEqual(waiting, json["waiting"].Value<bool>(), $"{topicName} の waiting が期待と違う");
            Assert.AreEqual(precedence, json["precedence"].Value<int>(), $"{topicName} の precedence が期待と違う");
        }
    }
}
