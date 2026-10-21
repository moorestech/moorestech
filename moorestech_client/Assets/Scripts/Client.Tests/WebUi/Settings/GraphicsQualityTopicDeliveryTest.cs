using System;
using System.Collections.Concurrent;
using System.Reflection;
using System.Threading.Tasks;
using Client.Tests.GraphicsQuality;
using Client.WebUiHost.Boot;
using Client.WebUiHost.Game.Actions;
using Client.WebUiHost.Game.Topics;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace Client.Tests.WebUi.Settings
{
    public class GraphicsQualityTopicDeliveryTest : GraphicsQualityTestScope
    {
        [Test]
        public async Task RegisteredActionPublishesChangedValuesToSubscribedConnection()
        {
            var hub = new WebSocketHub();
            SettingsActions.Register(hub);
            using var topic = new GraphicsQualityTopic(hub);
            hub.RegisterTopic(GraphicsQualityTopic.TopicName, topic);
            var connection = new WebSocketConnection(null);
            connection.Topics.TryAdd(GraphicsQualityTopic.TopicName, 0);
            ((ConcurrentDictionary<Guid, WebSocketConnection>)GetField(hub, "_connections")).TryAdd(Guid.NewGuid(), connection);
            var channel = GetField(connection, "_sendChannel");
            var reader = channel.GetType().GetProperty("Reader").GetValue(channel);
            var tryRead = reader.GetType().GetMethod("TryRead", BindingFlags.Instance | BindingFlags.Public);

            // snapshotの再取得ではなく購読接続へ届いたeventを観測する
            // Observe the event delivered to the subscribed connection instead of rereading a snapshot
            var result = await hub.ResolveAction("settings.applyGraphicsQualityPreset").ExecuteAsync(new JObject { ["preset"] = "low" });
            Assert.IsTrue(result.Ok);
            AssertEvent("low", 1, false, 50);
            result = await hub.ResolveAction("settings.setGraphicsQuality").ExecuteAsync(new JObject { ["item"] = "renderScalePercent", ["value"] = 75 });
            Assert.IsTrue(result.Ok);
            AssertEvent("custom", 2, false, 75);
            var empty = new object[] { null };
            Assert.IsFalse((bool)tryRead.Invoke(reader, empty));
            connection.RequestStop();

            #region Internal

            void AssertEvent(string preset, int revision, bool shadows, int scale)
            {
                var arguments = new object[] { null };
                Assert.IsTrue((bool)tryRead.Invoke(reader, arguments), "the action did not publish an event");
                var envelope = JObject.Parse((string)arguments[0]);
                Assert.AreEqual("event", envelope["op"].Value<string>());
                Assert.AreEqual(GraphicsQualityTopic.TopicName, envelope["topic"].Value<string>());
                Assert.AreEqual(revision, envelope["revision"].Value<int>());
                Assert.AreEqual(preset, envelope["data"]["preset"].Value<string>());
                Assert.AreEqual(shadows, envelope["data"]["settings"]["shadows"].Value<bool>());
                Assert.AreEqual(scale, envelope["data"]["settings"]["renderScalePercent"].Value<int>());
            }

            #endregion
        }
        [TestCase(false)][TestCase(true)]
        public async Task SameExplicitSelectionDeliversRecoveryAcknowledgment(bool preset)
        {
            var hub = new WebSocketHub();
            SettingsActions.Register(hub);
            using var topic = new GraphicsQualityTopic(hub);
            hub.RegisterTopic(GraphicsQualityTopic.TopicName, topic);
            UnityEngine.PlayerPrefs.SetString(Client.Game.InGame.GraphicsQuality.GraphicsQualityPreference.PreferenceKey, "broken");
            UnityEngine.TestTools.LogAssert.Expect(UnityEngine.LogType.Warning, new System.Text.RegularExpressions.Regex("Unknown saved graphics quality"));
            var snapshot = JObject.Parse(await topic.GetSnapshotJsonAsync());
            Assert.IsTrue(snapshot["recoveredInvalidPreference"].Value<bool>());
            snapshot = JObject.Parse(await topic.GetSnapshotJsonAsync());
            Assert.IsTrue(snapshot["recoveredInvalidPreference"].Value<bool>());
            Assert.AreEqual("medium", snapshot["preset"].Value<string>());
            var connection = new WebSocketConnection(null);
            try
            {
                connection.Topics.TryAdd(GraphicsQualityTopic.TopicName, 0);
                ((ConcurrentDictionary<Guid, WebSocketConnection>)GetField(hub, "_connections")).TryAdd(Guid.NewGuid(), connection);
                var channel = GetField(connection, "_sendChannel");
                var reader = channel.GetType().GetProperty("Reader").GetValue(channel);
                var tryRead = reader.GetType().GetMethod("TryRead", BindingFlags.Instance | BindingFlags.Public);

                // 同値でも解除通知が実購読接続へ届くことを確かめる
                // Verify acknowledgment reaches a real subscribed connection even for the same value
                var action = preset ? "settings.applyGraphicsQualityPreset" : "settings.setGraphicsQuality";
                var payload = preset ? new JObject { ["preset"] = "medium" } : new JObject { ["item"] = "shadows", ["value"] = true };
                Assert.IsTrue((await hub.ResolveAction(action).ExecuteAsync(payload)).Ok);
                var arguments = new object[] { null };
                Assert.IsTrue((bool)tryRead.Invoke(reader, arguments));
                var data = JObject.Parse((string)arguments[0])["data"];
                Assert.IsFalse(data["recoveredInvalidPreference"].Value<bool>());
                Assert.AreEqual("medium", data["preset"].Value<string>());
                Assert.IsTrue(JToken.DeepEquals(snapshot["settings"], data["settings"]));
                Assert.IsTrue((await hub.ResolveAction(action).ExecuteAsync(payload)).Ok);
                Assert.IsFalse((bool)tryRead.Invoke(reader, new object[] { null }));
            }
            finally { connection.RequestStop(); }
        }
    }
}
