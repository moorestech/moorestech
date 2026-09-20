using Client.WebUiHost.Boot;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace Client.Tests.WebUi.Gate
{
    // 開始を止めるゲートのtopicが登録され、待機状態を配っていることを検証する。未登録だとWeb側の購読が固着する
    // Asserts a start-holding gate topic is registered and publishes its waiting state; an unregistered topic wedges the web subscription
    internal static class StartGateTopicAssert
    {
        internal static void AssertWaiting(WebSocketHub hub, string topicName, bool waiting)
        {
            var topic = hub.ResolveTopic(topicName);
            Assert.IsNotNull(topic, $"{topicName} のtopicが登録されていない");
            var json = JObject.Parse(topic.GetSnapshotJsonAsync().GetAwaiter().GetResult());
            Assert.AreEqual(waiting, json["waiting"].Value<bool>(), $"{topicName} の waiting が期待と違う");
        }
    }
}
