using System;
using Client.Common;
using Client.Starter.EventMode;
using NUnit.Framework;

namespace Client.Tests.EventMode
{
    public class EventExhibitionModeTest
    {
        private const string EnableEnvKey = "MOORESTECH_EVENT_MODE";
        private const string EditorOptInEnvKey = "MOORESTECH_EVENT_MODE_EDITOR";
        private const string IdleTimeoutEnvKey = "MOORESTECH_EVENT_IDLE_TIMEOUT_SECONDS";
        private const string LanguageEnvKey = "MOORESTECH_EVENT_LANGUAGE";

        private static readonly string[] SavedEnvKeys = { EnableEnvKey, EditorOptInEnvKey, IdleTimeoutEnvKey, LanguageEnvKey };
        private readonly string[] savedEnvValues = new string[SavedEnvKeys.Length];

        [SetUp]
        public void SetUp()
        {
            // テスト前の環境変数を退避する
            // Save env vars as they were before the test
            for (var i = 0; i < SavedEnvKeys.Length; i++) savedEnvValues[i] = Environment.GetEnvironmentVariable(SavedEnvKeys[i]);
        }

        [TearDown]
        public void TearDown()
        {
            // 退避した値へ正確に書き戻す
            // Write the saved values back exactly
            for (var i = 0; i < SavedEnvKeys.Length; i++) Environment.SetEnvironmentVariable(SavedEnvKeys[i], savedEnvValues[i]);
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
            Environment.SetEnvironmentVariable(EnableEnvKey, "1");
            Environment.SetEnvironmentVariable(LanguageEnvKey, "german");

            Assert.AreEqual("german", EventExhibitionSettings.FromEnvironment().RequestedLanguageCode);
        }

        [Test]
        public void FromEnvironment_RequestedLanguageCode_IsNullWhenUnset()
        {
            Environment.SetEnvironmentVariable(EnableEnvKey, "1");
            Environment.SetEnvironmentVariable(LanguageEnvKey, null);

            Assert.IsNull(EventExhibitionSettings.FromEnvironment().RequestedLanguageCode);
        }
    }
}
