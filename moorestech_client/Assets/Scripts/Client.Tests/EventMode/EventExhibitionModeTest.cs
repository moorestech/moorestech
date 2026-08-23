using Client.Starter.EventMode;
using NUnit.Framework;

namespace Client.Tests.EventMode
{
    public class EventExhibitionModeTest
    {
        [Test]
        public void Parse_IsEnabled_AcceptsOnlyOne()
        {
            Assert.IsTrue(EventExhibitionSettings.Parse("1", null).IsEnabled);
            Assert.IsFalse(EventExhibitionSettings.Parse(null, null).IsEnabled);
            Assert.IsFalse(EventExhibitionSettings.Parse("", null).IsEnabled);
            Assert.IsFalse(EventExhibitionSettings.Parse("true", null).IsEnabled);
        }

        [Test]
        public void Parse_IdleTimeoutSeconds_AcceptsOnlyPositiveInt_DefaultsTo180()
        {
            Assert.AreEqual(180, EventExhibitionSettings.Parse("1", null).IdleTimeoutSeconds);
            Assert.AreEqual(60, EventExhibitionSettings.Parse("1", "60").IdleTimeoutSeconds);
            Assert.AreEqual(180, EventExhibitionSettings.Parse("1", "0").IdleTimeoutSeconds);
            Assert.AreEqual(180, EventExhibitionSettings.Parse("1", "-5").IdleTimeoutSeconds);
            Assert.AreEqual(180, EventExhibitionSettings.Parse("1", "abc").IdleTimeoutSeconds);
        }
    }
}
