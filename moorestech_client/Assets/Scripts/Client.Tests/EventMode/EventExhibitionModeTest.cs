using System;
using Client.Common;
using Client.Starter.EventMode;
using NUnit.Framework;

namespace Client.Tests.EventMode
{
    public class EventExhibitionModeTest
    {
        private const string EnableEnvKey = "MOORESTECH_EVENT_MODE";
        private const string LanguageEnvKey = "MOORESTECH_EVENT_LANGUAGE";

        [TearDown]
        public void TearDown()
        {
            // 環境変数はテストごとに元へ戻す
            // Restore env vars after each test
            Environment.SetEnvironmentVariable(EnableEnvKey, null);
            Environment.SetEnvironmentVariable(LanguageEnvKey, null);
        }

        private static EventExhibitionSettings Parse(string enable, string idleTimeout, string editorOptIn, bool isEditor)
        {
            return EventExhibitionSettings.Parse(new EventModeEnvironmentValues(enable, idleTimeout, editorOptIn, null), isEditor);
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
            Assert.AreEqual("german", EventExhibitionSettings.Parse(new EventModeEnvironmentValues("1", null, null, "german"), false).RequestedLanguageCode);
            Assert.IsNull(EventExhibitionSettings.Parse(new EventModeEnvironmentValues("1", null, null, null), false).RequestedLanguageCode);
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
