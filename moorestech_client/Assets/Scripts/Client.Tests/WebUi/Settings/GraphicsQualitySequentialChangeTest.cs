using System.Threading.Tasks;
using Client.Game.InGame.GraphicsQuality;
using Client.Tests.GraphicsQuality;
using Client.WebUiHost.Boot;
using Client.WebUiHost.Game.Actions;
using Client.WebUiHost.Game.Topics;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;

namespace Client.Tests.WebUi.Settings
{
    public class GraphicsQualitySequentialChangeTest : GraphicsQualityTestScope
    {
        [TestCase("drawDistanceMeters", 1000)][TestCase("renderScalePercent", 75)][TestCase("terrainDetail", "coarse")]
        [TestCase("shadows", false)][TestCase("ssao", false)][TestCase("postProcessing", false)]
        [TestCase("effects", false)][TestCase("mapObjects", false)][TestCase("ocean", false)]
        public async Task LatestHostValueSurvivesRapidChangesAndPresetThenItem(string item, object value)
        {
            var hub = new WebSocketHub();
            SettingsActions.Register(hub);
            using var topic = new GraphicsQualityTopic(hub);
            var action = hub.ResolveAction("settings.setGraphicsQuality");
            var initial = JObject.Parse(await topic.GetSnapshotJsonAsync());
            var expected = (JObject)initial["settings"].DeepClone();

            // topicを読む前に全9項目を順次送り、最後の項目だけ再操作する
            // Send all nine changes before reading a topic, then operate the selected item again
            var changes = JObject.Parse("{\"drawDistanceMeters\":1000,\"shadows\":false,\"renderScalePercent\":75,\"ssao\":false,\"postProcessing\":false,\"effects\":false,\"mapObjects\":false,\"terrainDetail\":\"coarse\",\"ocean\":false}");
            foreach (var field in changes.Properties())
            {
                var result = await action.ExecuteAsync(new JObject { ["item"] = field.Name, ["value"] = field.Value.DeepClone() });
                Assert.IsTrue(result.Ok, result.Error);
                expected[field.Name] = field.Value.DeepClone();
            }
            Assert.IsTrue((await action.ExecuteAsync(new JObject { ["item"] = item, ["value"] = JToken.FromObject(value) })).Ok);
            var snapshot = JObject.Parse(await topic.GetSnapshotJsonAsync());
            Assert.IsTrue(JToken.DeepEquals(expected, snapshot["settings"]), snapshot.ToString());
            Assert.AreEqual(GraphicsQualityPreference.Serialize(GraphicsQualityPreference.Current), PlayerPrefs.GetString(GraphicsQualityPreference.PreferenceKey));

            // 古い中のtopicを持ったまま低の直後に個別変更しても、他の低の値を保持する
            // Retain all untouched low values when changing an item immediately after a preset with a stale medium topic
            Assert.IsTrue((await hub.ResolveAction("settings.applyGraphicsQualityPreset").ExecuteAsync(new JObject { ["preset"] = "low" })).Ok);
            expected = JObject.Parse("{\"drawDistanceMeters\":300,\"shadows\":false,\"renderScalePercent\":50,\"ssao\":false,\"postProcessing\":false,\"effects\":false,\"mapObjects\":false,\"terrainDetail\":\"coarse\",\"ocean\":false}");
            var afterPresetValue = initial["settings"][item].DeepClone();
            Assert.IsTrue((await action.ExecuteAsync(new JObject { ["item"] = item, ["value"] = afterPresetValue })).Ok);
            expected[item] = afterPresetValue;
            snapshot = JObject.Parse(await topic.GetSnapshotJsonAsync());
            Assert.IsTrue(JToken.DeepEquals(expected, snapshot["settings"]), snapshot.ToString());
            Assert.AreEqual(GraphicsQualityPreference.Serialize(GraphicsQualityPreference.Current), PlayerPrefs.GetString(GraphicsQualityPreference.PreferenceKey));
        }
    }
}
