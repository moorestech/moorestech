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
        private static readonly string[] Codes = { "english", "japanese", "german" };

        [TearDown]
        public void TearDown()
        {
            // 環境変数はテストごとに元へ戻す
            // Restore env vars after each test
            Environment.SetEnvironmentVariable(EnableEnvKey, null);
            Environment.SetEnvironmentVariable(LanguageEnvKey, null);
        }

        [Test]
        public void Parse_LanguageCode_FallsBackToEnglishForUnsetOrUnknown()
        {
            Assert.AreEqual("english", EventExhibitionSettings.Parse("1", null, null, false, null, Codes).LanguageCode);
            Assert.AreEqual("english", EventExhibitionSettings.Parse("1", null, null, false, "", Codes).LanguageCode);
            Assert.AreEqual("german", EventExhibitionSettings.Parse("1", null, null, false, "german", Codes).LanguageCode);
            Assert.AreEqual("english", EventExhibitionSettings.Parse("1", null, null, false, "germn", Codes).LanguageCode);
        }

        [Test]
        public void Parse_IsEnabled_AcceptsOnlyOne()
        {
            Assert.IsTrue(EventExhibitionSettings.Parse("1", null, null, false, null, Codes).IsEnabled);
            Assert.IsFalse(EventExhibitionSettings.Parse(null, null, null, false, null, Codes).IsEnabled);
            Assert.IsFalse(EventExhibitionSettings.Parse("", null, null, false, null, Codes).IsEnabled);
            Assert.IsFalse(EventExhibitionSettings.Parse("true", null, null, false, null, Codes).IsEnabled);
        }

        [Test]
        public void Parse_IdleTimeoutSeconds_AcceptsOnlyPositiveInt_DefaultsTo180()
        {
            Assert.AreEqual(180, EventExhibitionSettings.Parse("1", null, null, false, null, Codes).IdleTimeoutSeconds);
            Assert.AreEqual(60, EventExhibitionSettings.Parse("1", "60", null, false, null, Codes).IdleTimeoutSeconds);
            Assert.AreEqual(180, EventExhibitionSettings.Parse("1", "0", null, false, null, Codes).IdleTimeoutSeconds);
            Assert.AreEqual(180, EventExhibitionSettings.Parse("1", "-5", null, false, null, Codes).IdleTimeoutSeconds);
            Assert.AreEqual(180, EventExhibitionSettings.Parse("1", "abc", null, false, null, Codes).IdleTimeoutSeconds);
        }

        [Test]
        public void Parse_InEditor_RequiresExplicitOptIn()
        {
            Assert.IsFalse(EventExhibitionSettings.Parse("1", null, null, true, null, Codes).IsEnabled);
            Assert.IsFalse(EventExhibitionSettings.Parse("1", null, "0", true, null, Codes).IsEnabled);
            Assert.IsTrue(EventExhibitionSettings.Parse("1", null, "1", true, null, Codes).IsEnabled);
            Assert.IsFalse(EventExhibitionSettings.Parse(null, null, "1", true, null, Codes).IsEnabled);
        }

        [Test]
        public void FromEnvironment_LanguageCode_ReadsEnvVariable()
        {
            Environment.SetEnvironmentVariable(EnableEnvKey, "1");
            Environment.SetEnvironmentVariable(LanguageEnvKey, "german");

            Assert.AreEqual("german", EventExhibitionSettings.FromEnvironment().LanguageCode);
        }

        [Test]
        public void FromEnvironment_LanguageCode_DefaultsToEnglishWhenUnset()
        {
            Environment.SetEnvironmentVariable(EnableEnvKey, "1");
            Environment.SetEnvironmentVariable(LanguageEnvKey, null);

            Assert.AreEqual("english", EventExhibitionSettings.FromEnvironment().LanguageCode);
        }

        [Test]
        public void ShouldRun_OnlyWhenEnabledAndOnMainMenu()
        {
            var enabled = EventExhibitionSettings.Parse("1", null, null, false, null, Codes);
            var disabled = EventExhibitionSettings.Parse(null, null, null, false, null, Codes);

            Assert.IsTrue(EventModeAutoStart.ShouldRun(enabled, SceneConstant.MainMenuSceneName));
            Assert.IsFalse(EventModeAutoStart.ShouldRun(disabled, SceneConstant.MainMenuSceneName));
            Assert.IsFalse(EventModeAutoStart.ShouldRun(enabled, SceneConstant.MainGameSceneName));
        }
    }
}
