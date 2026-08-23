using Client.Common;
using Client.Starter.EventMode;
using NUnit.Framework;

namespace Client.Tests.EventMode
{
    public class EventExhibitionModeTest
    {
        [Test]
        public void Parse_IsEnabled_AcceptsOnlyOne()
        {
            Assert.IsTrue(EventExhibitionSettings.Parse("1", null, null, false).IsEnabled);
            Assert.IsFalse(EventExhibitionSettings.Parse(null, null, null, false).IsEnabled);
            Assert.IsFalse(EventExhibitionSettings.Parse("", null, null, false).IsEnabled);
            Assert.IsFalse(EventExhibitionSettings.Parse("true", null, null, false).IsEnabled);
        }

        [Test]
        public void Parse_IdleTimeoutSeconds_AcceptsOnlyPositiveInt_DefaultsTo180()
        {
            Assert.AreEqual(180, EventExhibitionSettings.Parse("1", null, null, false).IdleTimeoutSeconds);
            Assert.AreEqual(60, EventExhibitionSettings.Parse("1", "60", null, false).IdleTimeoutSeconds);
            Assert.AreEqual(180, EventExhibitionSettings.Parse("1", "0", null, false).IdleTimeoutSeconds);
            Assert.AreEqual(180, EventExhibitionSettings.Parse("1", "-5", null, false).IdleTimeoutSeconds);
            Assert.AreEqual(180, EventExhibitionSettings.Parse("1", "abc", null, false).IdleTimeoutSeconds);
        }

        [Test]
        public void Parse_InEditor_RequiresExplicitOptIn()
        {
            Assert.IsFalse(EventExhibitionSettings.Parse("1", null, null, true).IsEnabled);
            Assert.IsFalse(EventExhibitionSettings.Parse("1", null, "0", true).IsEnabled);
            Assert.IsTrue(EventExhibitionSettings.Parse("1", null, "1", true).IsEnabled);
            Assert.IsFalse(EventExhibitionSettings.Parse(null, null, "1", true).IsEnabled);
        }

        [Test]
        public void ShouldRun_OnlyWhenEnabledAndOnMainMenu()
        {
            var enabled = EventExhibitionSettings.Parse("1", null, null, false);
            var disabled = EventExhibitionSettings.Parse(null, null, null, false);

            Assert.IsTrue(EventModeAutoStart.ShouldRun(enabled, SceneConstant.MainMenuSceneName));
            Assert.IsFalse(EventModeAutoStart.ShouldRun(disabled, SceneConstant.MainMenuSceneName));
            Assert.IsFalse(EventModeAutoStart.ShouldRun(enabled, SceneConstant.MainGameSceneName));
        }
    }
}
