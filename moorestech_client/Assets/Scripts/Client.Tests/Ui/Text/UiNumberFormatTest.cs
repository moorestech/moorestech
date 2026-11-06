using System.Globalization;
using Client.Ui.State.Text;
using NUnit.Framework;

namespace Client.Tests.Ui.Text
{
    /// <summary>
    /// 現在のカルチャが de-DE でも小数点が「.」・桁区切りなしになることを確かめる（L0）
    /// Verifies '.' decimals and no grouping even when the current culture is de-DE (L0)
    /// </summary>
    public class UiNumberFormatTest
    {
        private CultureInfo _savedCulture;

        [SetUp]
        public void SetUp()
        {
            _savedCulture = CultureInfo.CurrentCulture;
            CultureInfo.CurrentCulture = new CultureInfo("de-DE");
        }

        [TearDown]
        public void TearDown()
        {
            CultureInfo.CurrentCulture = _savedCulture;
        }

        [Test]
        public void FixedUsesDotAndNoGroupingUnderGermanCulture()
        {
            Assert.AreEqual("1234.5", UiNumberFormat.Fixed(1234.5, 1));
        }

        [Test]
        public void FixedRoundsHalfAwayFromZeroLikeToFixed()
        {
            Assert.AreEqual("0.13", UiNumberFormat.Fixed(0.125, 2));
        }

        [Test]
        public void IntegerHasNoGrouping()
        {
            Assert.AreEqual("1234567", UiNumberFormat.Integer(1234567));
        }

        [Test]
        public void RoundHalfUpMatchesWebMathRound()
        {
            // Web の Math.round(88.888)=89・Math.round(2.5)=3・Math.round(-2.5)=-2 と同じ
            // Same as Web Math.round(88.888)=89, Math.round(2.5)=3, Math.round(-2.5)=-2
            Assert.AreEqual(89, UiNumberFormat.RoundHalfUp(88.888));
            Assert.AreEqual(3, UiNumberFormat.RoundHalfUp(2.5));
            Assert.AreEqual(-2, UiNumberFormat.RoundHalfUp(-2.5));
        }
    }
}
