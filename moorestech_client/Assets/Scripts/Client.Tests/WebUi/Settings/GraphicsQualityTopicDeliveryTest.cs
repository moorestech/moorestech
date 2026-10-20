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
            var settings = JObject.Parse("{\"drawDistanceMeters\":1000,\"shadows\":true,\"renderScalePercent\":75,\"ssao\":true,\"postProcessing\":true,\"effects\":true,\"mapObjects\":true,\"terrainDetail\":\"fine\",\"ocean\":true}");
            result = await hub.ResolveAction("settings.setGraphicsQuality").ExecuteAsync(new JObject { ["settings"] = settings });
            Assert.IsTrue(result.Ok);
            AssertEvent("custom", 2, true, 75);
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
    }
}
