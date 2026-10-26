using Client.Localization;
using NUnit.Framework;

namespace Client.Tests.Localization.Resolution
{
    public class LocalizationTextInterpolatorTest
    {
        [Test]
        public void ValueContainingBracesIsNotReinterpolated()
        {
            // 差し込んだ値の中の{...}は再置換しない
            // Braces inside an inserted value are not interpolated again
            var text = LocalizationTextInterpolator.Interpolate("test.key", "{a}/{b}", new[]
            {
                new LocalizationTextParam("a", "{b}"),
                new LocalizationTextParam("b", "2"),
            });

            Assert.AreEqual("{b}/2", text);
        }
    }
}
