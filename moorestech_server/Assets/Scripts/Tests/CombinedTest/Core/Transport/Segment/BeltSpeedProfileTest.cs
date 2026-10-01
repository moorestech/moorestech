using System;
using System.Globalization;
using Game.Block.Blocks.BeltConveyor.Transport;
using NUnit.Framework;

namespace Tests.CombinedTest.Core.Transport.Segment
{
    public class BeltSpeedProfileTest
    {
        [Test]
        public void DistinctConfigurationFieldsNeverCollapseToEqualProductsTest()
        {
            // 積が一致しても設定値そのものを保ち、浮動小数演算の同値推測をしない。
            // Preserve configured values even when products match instead of inferring floating-point equivalence.
            Assert.AreNotEqual(BeltTransportSpeedProfile.Gear(0.4, 10, 0), BeltTransportSpeedProfile.Gear(0.2, 20, 0));
            Assert.AreNotEqual(BeltTransportSpeedProfile.Gear(0.4, 10, 0), BeltTransportSpeedProfile.Gear(0.4, 10, 1));
            Assert.AreNotEqual(BeltTransportSpeedProfile.Fixed(0.4), BeltTransportSpeedProfile.Gear(0.4, 10, 0));
            double adjacent = BitConverter.Int64BitsToDouble(BitConverter.DoubleToInt64Bits(0.4) + 1);
            Assert.AreNotEqual(BeltTransportSpeedProfile.Fixed(0.4), BeltTransportSpeedProfile.Fixed(adjacent));
        }

        [Test]
        public void ProfileIdentityIsIndependentOfProcessCultureTest()
        {
            var previous = CultureInfo.CurrentCulture;
            var expected = BeltTransportSpeedProfile.Gear(0.375, 12.5, 0.25);
            try
            {
                CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("fr-FR");
                Assert.AreEqual(expected, BeltTransportSpeedProfile.Gear(0.375, 12.5, 0.25));
                CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("ja-JP");
                Assert.AreEqual(expected, BeltTransportSpeedProfile.Gear(0.375, 12.5, 0.25));
            }
            finally { CultureInfo.CurrentCulture = previous; }
        }
    }
}
