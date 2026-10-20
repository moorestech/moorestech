using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Client.Game.InGame.GraphicsQuality;
using Client.WebUiHost.Boot;
using Client.WebUiHost.Common;
using Client.WebUiHost.Game.Actions;
using Client.WebUiHost.Game.Topics;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Client.Tests.WebUi.Settings
{
    public class GraphicsQualityContractTest
    {
        private bool _hadSaved;
        private string _saved;

        [SetUp]
        public void SetUp()
        {
            // 開発者の保存値を退避し未保存から始める
            // Stash the developer preference and start unsaved
            _hadSaved = PlayerPrefs.HasKey(GraphicsQualityPreference.PreferenceKey);
            _saved = PlayerPrefs.GetString(GraphicsQualityPreference.PreferenceKey);
            PlayerPrefs.DeleteKey(GraphicsQualityPreference.PreferenceKey);
        }

        [TearDown]
        public void TearDown()
        {
            // 保存値を復元して他テストへ漏らさない
            // Restore persistence to avoid leaking into other tests
            if (_hadSaved) PlayerPrefs.SetString(GraphicsQualityPreference.PreferenceKey, _saved);
            else PlayerPrefs.DeleteKey(GraphicsQualityPreference.PreferenceKey);
            PlayerPrefs.Save();
        }

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
            Assert.IsFalse(snapshot["settings"]?["shadows"]?.Value<bool>());
        }

        [Test]
        public async Task UnsavedTopicReportsMedium()
        {
            var hub = new WebSocketHub();
            using var topic = new GraphicsQualityTopic(hub);
            var snapshot = JObject.Parse(await topic.GetSnapshotJsonAsync());
            Assert.AreEqual("medium", snapshot["preset"]?.Value<string>());
        }

        [Test]
        public async Task ChangingOneItemAfterPresetReportsCustom()
        {
            var hub = new WebSocketHub();
            SettingsActions.Register(hub);
            using var topic = new GraphicsQualityTopic(hub);
            var settings = MediumSettingsJson();
            settings["shadows"] = false;
            var result = await hub.ResolveAction("settings.setGraphicsQuality").ExecuteAsync(new JObject { ["settings"] = settings });
            var snapshot = JObject.Parse(await topic.GetSnapshotJsonAsync());
            Assert.IsTrue(result.Ok);
            Assert.AreEqual("custom", snapshot["preset"]?.Value<string>());
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
        [TestCase("drawDistanceMeters", 777)]
        [TestCase("drawDistanceMeters", "300")]
        [TestCase("drawDistanceMeters", 300.5)]
        [TestCase("renderScalePercent", 99999999999L)]
        [TestCase("shadows", "true")]
        [TestCase("terrainDetail", "ultra")]
        [TestCase("ocean", null)]
        public async Task InvalidSettingsAreRejectedWithoutSaving(string key, object value)
        {
            LogAssert.Expect(LogType.Warning, new Regex("Rejected invalid graphics quality"));
            var settings = MediumSettingsJson();
            settings[key] = value == null ? null : JToken.FromObject(value);
            var result = await new SetGraphicsQualityActionHandler().ExecuteAsync(new JObject { ["settings"] = settings });
            Assert.IsFalse(result.Ok);
            Assert.AreEqual("invalid_graphics_quality", result.Error);
            Assert.IsFalse(PlayerPrefs.HasKey(GraphicsQualityPreference.PreferenceKey));
        }

        [Test]
        public async Task OversizedIntegerBeyondLongIsRejectedWithoutThrowing()
        {
            LogAssert.Expect(LogType.Warning, new Regex("Rejected invalid graphics quality"));
            var json = "{\"settings\":" + MediumSettingsJson().ToString().Replace("\"drawDistanceMeters\": 300", "\"drawDistanceMeters\": 99999999999999999999999") + "}";
            var result = await new SetGraphicsQualityActionHandler().ExecuteAsync(JObject.Parse(json));
            Assert.IsFalse(result.Ok);
            Assert.AreEqual("invalid_graphics_quality", result.Error);
            Assert.IsFalse(PlayerPrefs.HasKey(GraphicsQualityPreference.PreferenceKey));
        }

        private static JObject MediumSettingsJson()
        {
            return JObject.Parse(WebUiJson.Serialize(GraphicsQualityWire.ToData(GraphicsQualityPresets.Get(GraphicsQualityPreset.Medium))));
        }
    }
}
