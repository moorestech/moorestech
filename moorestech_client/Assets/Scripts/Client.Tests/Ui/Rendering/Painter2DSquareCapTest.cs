using Client.Ui.Foundation.Rendering;
using NUnit.Framework;
using UnityEngine;

namespace Client.Tests.Ui.Rendering
{
    public class Painter2DSquareCapTest
    {
        [Test]
        public void ExtendPushesTheEndAwayFromTheOtherEndByLength()
        {
            var extended = Painter2DSquareCap.Extend(new Vector2(10f, 0f), Vector2.zero, 2f);
            Assert.AreEqual(12f, extended.x, 0.0001f);
            Assert.AreEqual(0f, extended.y, 0.0001f);
        }
    }
}
