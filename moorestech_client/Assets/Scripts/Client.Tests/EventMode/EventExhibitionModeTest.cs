using System;
using System.Linq;
using Client.Common;
using Client.Starter.EventMode;
using NUnit.Framework;

namespace Client.Tests.EventMode
{
    public class EventExhibitionModeTest
    {
        // 有効化キーは共通の集合から取り、有効化キーが増えても退避漏れが起きないようにする
        // Take the enabling keys from the shared set so a new enabling key is never left unsaved
        private static readonly string[] SavedEnvKeys = EventModeTestEnvironment.ExhibitionEnableKeys
            .Concat(new[] { EventExhibitionSettings.IdleTimeoutEnvKey, EventExhibitionSettings.LanguageEnvKey }).ToArray();
        private string[] savedEnvValues;

        [SetUp]
        public void SetUp()
        {
            savedEnvValues = EventModeTestEnvironment.Capture(SavedEnvKeys);
        }

        [TearDown]
        public void TearDown()
        {
            EventModeTestEnvironment.Restore(SavedEnvKeys, savedEnvValues);
        }

        private static EventExhibitionSettings Parse(string enable, string idleTimeout, string editorOptIn, bool isEditor)
        {
            return ParseWithLanguage(enable, idleTimeout, editorOptIn, null, isEditor);
        }

        private static EventExhibitionSettings ParseWithLanguage(string enable, string idleTimeout, string editorOptIn, string language, bool isEditor)
        {
            var raw = new EventModeEnvironmentValues
            {
                Enable = enable,
                IdleTimeoutSeconds = idleTimeout,
                EditorOptIn = editorOptIn,
                Language = language,
            };
            return EventExhibitionSettings.Parse(raw, isEditor);
        }

        [Test]
        public void Parse_IsEnabled_AcceptsOnlyOne()
        {
            Assert.IsTrue(Parse("1", null, null, false).IsEnabled);
            Assert.IsFalse(Parse(null, null, null, false).IsEnabled);
            Assert.IsFalse(Parse("", null, null, false).IsEnabled);
            Assert.IsFalse(Parse("true", null, null, false).IsEnabled);
        }

        [Test]
        public void Parse_IdleTimeoutSeconds_AcceptsOnlyPositiveInt_DefaultsTo180()
        {
            Assert.AreEqual(180, Parse("1", null, null, false).IdleTimeoutSeconds);
            Assert.AreEqual(60, Parse("1", "60", null, false).IdleTimeoutSeconds);
            Assert.AreEqual(180, Parse("1", "0", null, false).IdleTimeoutSeconds);
            Assert.AreEqual(180, Parse("1", "-5", null, false).IdleTimeoutSeconds);
            Assert.AreEqual(180, Parse("1", "abc", null, false).IdleTimeoutSeconds);
        }

        [Test]
        public void Parse_InEditor_RequiresExplicitOptIn()
        {
            Assert.IsFalse(Parse("1", null, null, true).IsEnabled);
            Assert.IsFalse(Parse("1", null, "0", true).IsEnabled);
            Assert.IsTrue(Parse("1", null, "1", true).IsEnabled);
            Assert.IsFalse(Parse(null, null, "1", true).IsEnabled);
        }

        [Test]
        public void Parse_RequestedLanguageCode_PassesRawValueThrough()
        {
            Assert.AreEqual("german", ParseWithLanguage("1", null, null, "german", false).RequestedLanguageCode);
            Assert.IsNull(ParseWithLanguage("1", null, null, null, false).RequestedLanguageCode);
        }

        [Test]
        public void ShouldRun_OnlyWhenEnabledAndOnMainMenu()
        {
            var enabled = Parse("1", null, null, false);
            var disabled = Parse(null, null, null, false);

            Assert.IsTrue(EventModeAutoStart.ShouldRun(enabled, SceneConstant.MainMenuSceneName));
            Assert.IsFalse(EventModeAutoStart.ShouldRun(disabled, SceneConstant.MainMenuSceneName));
            Assert.IsFalse(EventModeAutoStart.ShouldRun(enabled, SceneConstant.MainGameSceneName));
        }

        [Test]
        public void FromEnvironment_RequestedLanguageCode_ReadsEnvVariable()
        {
            Environment.SetEnvironmentVariable(EventExhibitionSettings.EnableEnvKey, "1");
            Environment.SetEnvironmentVariable(EventExhibitionSettings.LanguageEnvKey, "german");

            Assert.AreEqual("german", EventExhibitionSettings.FromEnvironment().RequestedLanguageCode);
        }

        [Test]
        public void FromEnvironment_RequestedLanguageCode_IsNullWhenUnset()
        {
            Environment.SetEnvironmentVariable(EventExhibitionSettings.EnableEnvKey, "1");
            Environment.SetEnvironmentVariable(EventExhibitionSettings.LanguageEnvKey, null);

            Assert.IsNull(EventExhibitionSettings.FromEnvironment().RequestedLanguageCode);
        }
    }
}
