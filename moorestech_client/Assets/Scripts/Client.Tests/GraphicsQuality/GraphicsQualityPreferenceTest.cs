using System.Text.RegularExpressions;
using Client.Game.InGame.GraphicsQuality;
using NUnit.Framework;
using UniRx;
using UnityEngine;
using UnityEngine.TestTools;

namespace Client.Tests.GraphicsQuality
{
    public class GraphicsQualityPreferenceTest
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
            if (_hadSaved) PlayerPrefs.SetString(GraphicsQualityPreference.PreferenceKey, _saved);
            else PlayerPrefs.DeleteKey(GraphicsQualityPreference.PreferenceKey);
            PlayerPrefs.Save();
        }

        [Test]
        public void UnsavedPreferenceStartsAtMedium()
        {
            AssertFixed(GraphicsQualityPreference.Current, GraphicsDrawDistance.Meters300, GraphicsRenderScale.Percent100, GraphicsTerrainDetail.Fine, true);
        }

        [Test]
        public void SavedSettingsRoundTripAndNotifyOnlyOnChange()
        {
            var changes = 0;
            using var subscription = GraphicsQualityPreference.OnSettingsChanged.Subscribe(_ => changes++);
            var custom = new GraphicsQualitySettings(GraphicsDrawDistance.Meters1000, false, GraphicsRenderScale.Percent75, true, false, true, false, GraphicsTerrainDetail.Coarse, true);

            // 同じ値の再保存では通知しない
            // Re-saving the same value does not notify
            GraphicsQualityPreference.SetSettings(custom);
            GraphicsQualityPreference.SetSettings(custom);
            Assert.AreEqual(custom, GraphicsQualityPreference.Current);
            Assert.AreEqual(1, changes);
            Assert.IsFalse(GraphicsQualityPresets.TryMatch(custom, out _));
        }

        [Test]
        public void ApplyPresetOverwritesEveryItem()
        {
            GraphicsQualityPreference.ApplyPreset(GraphicsQualityPreset.Low);
            AssertFixed(GraphicsQualityPreference.Current, GraphicsDrawDistance.Meters300, GraphicsRenderScale.Percent50, GraphicsTerrainDetail.Coarse, false);
            Assert.IsTrue(GraphicsQualityPresets.TryMatch(GraphicsQualityPreference.Current, out var preset));
            Assert.AreEqual(GraphicsQualityPreset.Low, preset);
        }

        [TestCase("garbage")]
        [TestCase("Meters300|true|Percent100|true|true|true|true|Fine")]
        [TestCase("Meters999|true|Percent100|true|true|true|true|Fine|true")]
        public void InvalidSavedValueIsRepairedToMediumWithWarning(string saved)
        {
            PlayerPrefs.SetString(GraphicsQualityPreference.PreferenceKey, saved);
            LogAssert.Expect(LogType.Warning, new Regex(@"\[GraphicsQualityPreference\] Unknown saved graphics quality"));
            AssertFixed(GraphicsQualityPreference.Current, GraphicsDrawDistance.Meters300, GraphicsRenderScale.Percent100, GraphicsTerrainDetail.Fine, true);
            Assert.AreEqual("Meters300|True|Percent100|True|True|True|True|Fine|True", PlayerPrefs.GetString(GraphicsQualityPreference.PreferenceKey));
        }
        [TestCase(GraphicsQualityPreset.High, GraphicsDrawDistance.Meters5000, GraphicsRenderScale.Percent100, GraphicsTerrainDetail.Fine, true)]
        [TestCase(GraphicsQualityPreset.Medium, GraphicsDrawDistance.Meters300, GraphicsRenderScale.Percent100, GraphicsTerrainDetail.Fine, true)]
        [TestCase(GraphicsQualityPreset.Low, GraphicsDrawDistance.Meters300, GraphicsRenderScale.Percent50, GraphicsTerrainDetail.Coarse, false)]
        public void PresetsHaveFixedIndependentValues(GraphicsQualityPreset preset, GraphicsDrawDistance distance, GraphicsRenderScale scale, GraphicsTerrainDetail terrain, bool enabled)
        {
            AssertFixed(GraphicsQualityPresets.Get(preset), distance, scale, terrain, enabled);
        }

        [TestCase(0)][TestCase(1)][TestCase(2)][TestCase(3)][TestCase(4)]
        [TestCase(5)][TestCase(6)][TestCase(7)][TestCase(8)]
        public void EachItemPersistsNotifiesAndMatchesIndependently(int index)
        {
            var values = new[] { "Meters300", "True", "Percent100", "True", "True", "True", "True", "Fine", "True" };
            values[index] = index switch { 0 => "Meters1000", 2 => "Percent75", 7 => "Coarse", _ => "False" };
            var expected = new GraphicsQualitySettings(index == 0 ? GraphicsDrawDistance.Meters1000 : GraphicsDrawDistance.Meters300,
                index != 1, index == 2 ? GraphicsRenderScale.Percent75 : GraphicsRenderScale.Percent100,
                index != 3, index != 4, index != 5, index != 6, index == 7 ? GraphicsTerrainDetail.Coarse : GraphicsTerrainDetail.Fine, index != 8);
            var notifications = 0;
            using var subscription = GraphicsQualityPreference.OnSettingsChanged.Subscribe(_ => notifications++);

            // 一項目ずつ保存・通知・カスタム判定の独立性を確認する
            // Check persistence, notification and custom matching for each individual item
            GraphicsQualityPreference.SetSettings(expected);
            GraphicsQualityPreference.SetSettings(expected);
            Assert.AreEqual(string.Join("|", values), PlayerPrefs.GetString(GraphicsQualityPreference.PreferenceKey));
            Assert.AreEqual(1, notifications);
            Assert.IsFalse(GraphicsQualityPresets.TryMatch(GraphicsQualityPreference.Current, out _));
            GraphicsQualityPreference.ApplyPreset(GraphicsQualityPreset.Medium);
            Assert.AreEqual(2, notifications);
            Assert.IsTrue(GraphicsQualityPresets.TryMatch(GraphicsQualityPreference.Current, out var preset));
            Assert.AreEqual(GraphicsQualityPreset.Medium, preset);
            AssertFixed(GraphicsQualityPreference.Current, GraphicsDrawDistance.Meters300, GraphicsRenderScale.Percent100, GraphicsTerrainDetail.Fine, true);
        }

        private static void AssertFixed(GraphicsQualitySettings actual, GraphicsDrawDistance distance, GraphicsRenderScale scale, GraphicsTerrainDetail terrain, bool enabled)
        {
            Assert.AreEqual(distance, actual.DrawDistance);
            Assert.AreEqual(scale, actual.RenderScale);
            Assert.AreEqual(terrain, actual.TerrainDetail);
            Assert.AreEqual(enabled, actual.ShadowsEnabled);
            Assert.AreEqual(enabled, actual.SsaoEnabled);
            Assert.AreEqual(enabled, actual.PostProcessingEnabled);
            Assert.AreEqual(enabled, actual.EffectsEnabled);
            Assert.AreEqual(enabled, actual.MapObjectsVisible);
            Assert.AreEqual(enabled, actual.OceanVisible);
        }
    }
}
