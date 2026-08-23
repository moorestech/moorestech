using Client.Starter.EventMode;
using NUnit.Framework;

namespace Client.Tests.EventMode
{
    public class EventExhibitionModeTest
    {
        [Test]
        public void IsEnabledValue_AcceptsOnlyOne()
        {
            Assert.IsTrue(EventExhibitionMode.IsEnabledValue("1"));
            Assert.IsFalse(EventExhibitionMode.IsEnabledValue(null));
            Assert.IsFalse(EventExhibitionMode.IsEnabledValue(""));
            Assert.IsFalse(EventExhibitionMode.IsEnabledValue("true"));
        }

        [Test]
        public void ParseIdleTimeoutSeconds_AcceptsOnlyPositiveInt_DefaultsTo180()
        {
            Assert.AreEqual(180, EventExhibitionMode.ParseIdleTimeoutSeconds(null));
            Assert.AreEqual(60, EventExhibitionMode.ParseIdleTimeoutSeconds("60"));
            Assert.AreEqual(180, EventExhibitionMode.ParseIdleTimeoutSeconds("0"));
            Assert.AreEqual(180, EventExhibitionMode.ParseIdleTimeoutSeconds("-5"));
            Assert.AreEqual(180, EventExhibitionMode.ParseIdleTimeoutSeconds("abc"));
        }
    }
}
