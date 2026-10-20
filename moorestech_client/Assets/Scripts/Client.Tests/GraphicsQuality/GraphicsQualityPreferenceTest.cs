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
            Assert.AreEqual(GraphicsQualityPresets.Get(GraphicsQualityPreset.Medium), GraphicsQualityPreference.Current);
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
            Assert.AreEqual(GraphicsQualityPresets.Get(GraphicsQualityPreset.Low), GraphicsQualityPreference.Current);
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
            Assert.AreEqual(GraphicsQualityPresets.Get(GraphicsQualityPreset.Medium), GraphicsQualityPreference.Current);
            Assert.AreEqual(GraphicsQualityPreference.Serialize(GraphicsQualityPresets.Get(GraphicsQualityPreset.Medium)), PlayerPrefs.GetString(GraphicsQualityPreference.PreferenceKey));
        }
    }
}
