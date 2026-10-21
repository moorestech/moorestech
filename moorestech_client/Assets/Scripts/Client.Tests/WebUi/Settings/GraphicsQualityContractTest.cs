using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Client.Game.InGame.GraphicsQuality;
using Client.WebUiHost.Boot;
using Client.WebUiHost.Game.Actions;
using Client.WebUiHost.Game.Topics;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Client.Tests.WebUi.Settings
{
    public class GraphicsQualityContractTest : Client.Tests.GraphicsQuality.GraphicsQualityTestScope
    {
        [Test]
        public async Task PresetActionPersistsAndTopicReportsPresetName()
        {
            var hub = new WebSocketHub();
            SettingsActions.Register(hub);
            using var topic = new GraphicsQualityTopic(hub);
            var result = await hub.ResolveAction("settings.applyGraphicsQualityPreset").ExecuteAsync(new JObject { ["preset"] = "low" });
            var snapshot = JObject.Parse(await topic.GetSnapshotJsonAsync());
            Assert.IsTrue(result.Ok);
            Assert.AreEqual("low", snapshot["preset"]?.Value<string>());
            Assert.AreEqual(300, snapshot["settings"]?["drawDistanceMeters"]?.Value<int>());
            var expected = MediumSettingsJson();
            foreach (var key in new[] { "shadows", "ssao", "postProcessing", "effects", "mapObjects", "ocean" }) expected[key] = false;
            expected["renderScalePercent"] = 50;
            expected["terrainDetail"] = "coarse";
            Assert.IsTrue(JToken.DeepEquals(expected, snapshot["settings"]), snapshot.ToString());
            Assert.AreEqual(1, hub.GetTopicRevision(GraphicsQualityTopic.TopicName));
        }

        [Test]
        public async Task UnsavedTopicReportsMedium()
        {
            var hub = new WebSocketHub();
            using var topic = new GraphicsQualityTopic(hub);
            var snapshot = JObject.Parse(await topic.GetSnapshotJsonAsync());
            Assert.AreEqual("medium", snapshot["preset"]?.Value<string>());
            Assert.IsFalse(snapshot["recoveredInvalidPreference"].Value<bool>());
            Assert.IsTrue(JToken.DeepEquals(MediumSettingsJson(), snapshot["settings"]));
        }

        [TestCase("drawDistanceMeters", 1000)][TestCase("renderScalePercent", 75)][TestCase("terrainDetail", "coarse")]
        [TestCase("shadows", false)][TestCase("ssao", false)][TestCase("postProcessing", false)]
        [TestCase("effects", false)][TestCase("mapObjects", false)][TestCase("ocean", false)]
        public async Task ChangingOneItemAfterPresetReportsCustom(string key, object value)
        {
            var hub = new WebSocketHub();
            SettingsActions.Register(hub);
            using var topic = new GraphicsQualityTopic(hub);
            var settings = MediumSettingsJson();
            settings[key] = JToken.FromObject(value);
            var result = await hub.ResolveAction("settings.setGraphicsQuality").ExecuteAsync(new JObject { ["item"] = key, ["value"] = JToken.FromObject(value) });
            var snapshot = JObject.Parse(await topic.GetSnapshotJsonAsync());
            Assert.IsTrue(result.Ok);
            Assert.AreEqual("custom", snapshot["preset"]?.Value<string>());
            Assert.IsTrue(JToken.DeepEquals(settings, snapshot["settings"]));
            Assert.AreEqual(1, hub.GetTopicRevision(GraphicsQualityTopic.TopicName));
        }

        [TestCase("ultra")]
        [TestCase(null)]
        public async Task UnknownPresetIsRejectedWithoutSaving(string wire)
        {
            LogAssert.Expect(LogType.Warning, new Regex("Rejected unknown graphics quality preset"));
            var result = await new ApplyGraphicsQualityPresetActionHandler().ExecuteAsync(new JObject { ["preset"] = wire });
            Assert.IsFalse(result.Ok);
            Assert.AreEqual("unknown_graphics_quality_preset", result.Error);
            Assert.IsFalse(PlayerPrefs.HasKey(GraphicsQualityPreference.PreferenceKey));
        }

        // 未定義値・型違い・longを超える整数・小数・欠落はすべて例外にならず失敗契約で返る
        // Undefined values, wrong types, integers beyond long, fractions and missing keys all return the failure contract without throwing
        [TestCase("drawDistanceMeters", 777, "unsupported_value")]
        [TestCase("drawDistanceMeters", "300", "invalid_type")]
        [TestCase("drawDistanceMeters", 300.5, "invalid_type")]
        [TestCase("renderScalePercent", 99999999999L, "unsupported_value")]
        [TestCase("shadows", "true", "invalid_type")]
        [TestCase("terrainDetail", "ultra", "unsupported_value")]
        [TestCase("ocean", null, "invalid_type")]
        [TestCase("ssao", 1, "invalid_type")][TestCase("postProcessing", "false", "invalid_type")]
        [TestCase("effects", 0, "invalid_type")][TestCase("mapObjects", "on", "invalid_type")]
        [TestCase("terrainDetail", 1, "invalid_type")][TestCase("renderScalePercent", 75.5, "invalid_type")]
        [TestCase("unknown", true, "unsupported_value")]
        [TestCase("", true, "unsupported_value")]
        public async Task InvalidSettingsAreRejectedWithoutSaving(string key, object value, string reason)
        {
            LogAssert.Expect(LogType.Warning, new Regex("Rejected invalid graphics quality"));

            var result = await new SetGraphicsQualityActionHandler().ExecuteAsync(new JObject { ["item"] = key, ["value"] = value == null ? null : JToken.FromObject(value) });
            Assert.IsFalse(result.Ok);
            Assert.AreEqual($"invalid_graphics_quality:{reason}:{(key is "unknown" or "" ? "item" : key)}", result.Error);
            Assert.IsFalse(PlayerPrefs.HasKey(GraphicsQualityPreference.PreferenceKey));
        }

        [Test]
        public async Task OversizedIntegerBeyondLongIsRejectedWithoutThrowing()
        {
            LogAssert.Expect(LogType.Warning, new Regex("Rejected invalid graphics quality"));
            var json = "{\"item\":\"drawDistanceMeters\",\"value\":99999999999999999999999}";
            var result = await new SetGraphicsQualityActionHandler().ExecuteAsync(JObject.Parse(json));
            Assert.IsFalse(result.Ok);
            Assert.AreEqual("invalid_graphics_quality:unsupported_value:drawDistanceMeters", result.Error);
            Assert.IsFalse(PlayerPrefs.HasKey(GraphicsQualityPreference.PreferenceKey));
        }

        [TestCase(false)][TestCase(true)]
        public async Task DirectBoxedIntegersAndParsedJsonHaveTheSameContract(bool parseJson)
        {
            var payload = new JObject { ["item"] = "drawDistanceMeters", ["value"] = 300 };
            if (parseJson) payload = JObject.Parse(payload.ToString());
            var result = await new SetGraphicsQualityActionHandler().ExecuteAsync(payload);
            Assert.IsTrue(result.Ok, result.Error);
            var topic = new GraphicsQualityTopic(new WebSocketHub());
            using (topic)
            {
                var snapshot = JObject.Parse(await topic.GetSnapshotJsonAsync());
                Assert.IsTrue(JToken.DeepEquals(MediumSettingsJson(), snapshot["settings"]));
            }
        }

        [TestCase("drawDistanceMeters")][TestCase("renderScalePercent")][TestCase("terrainDetail")]
        [TestCase("shadows")][TestCase("ssao")][TestCase("postProcessing")]
        [TestCase("effects")][TestCase("mapObjects")][TestCase("ocean")]
        public async Task MissingFieldPreservesItsNameAndExistingSavedValue(string key)
        {
            GraphicsQualityPreference.ApplyPreset(GraphicsQualityPreset.Low);
            var saved = PlayerPrefs.GetString(GraphicsQualityPreference.PreferenceKey);
            LogAssert.Expect(LogType.Warning, new Regex("Rejected invalid graphics quality"));
            var result = await new SetGraphicsQualityActionHandler().ExecuteAsync(new JObject { ["item"] = key });
            Assert.AreEqual($"invalid_graphics_quality:missing_field:{key}", result.Error);
            Assert.AreEqual(saved, PlayerPrefs.GetString(GraphicsQualityPreference.PreferenceKey));
        }

        [TestCase(false)][TestCase(true)]
        public async Task ItemDiscriminatorDistinguishesMissingAndWrongType(bool wrongType)
        {
            LogAssert.Expect(LogType.Warning, new Regex("Rejected invalid graphics quality"));
            var payload = wrongType ? new JObject { ["item"] = false, ["value"] = true } : new JObject();
            var result = await new SetGraphicsQualityActionHandler().ExecuteAsync(payload);
            Assert.AreEqual(wrongType ? "invalid_graphics_quality:invalid_type:item" : "invalid_graphics_quality:missing_field:item", result.Error);
            Assert.IsFalse(PlayerPrefs.HasKey(GraphicsQualityPreference.PreferenceKey));
        }

        private static JObject MediumSettingsJson()
        {
            return new JObject
            {
                ["drawDistanceMeters"] = 300, ["shadows"] = true, ["renderScalePercent"] = 100,
                ["ssao"] = true, ["postProcessing"] = true, ["effects"] = true, ["mapObjects"] = true,
                ["terrainDetail"] = "fine", ["ocean"] = true,
            };
        }
    }
}
