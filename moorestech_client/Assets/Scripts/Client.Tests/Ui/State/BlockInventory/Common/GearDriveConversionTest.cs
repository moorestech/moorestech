using Client.Ui.State.BlockInventory.Common;
using Game.Block.Interface.State;
using Game.Gear.Common;
using NUnit.Framework;

namespace Client.Tests.Ui.State.BlockInventory.Common
{
    /// <summary>
    /// 歯車駆動の判定は純関数で、異常はログでなく結果に載る
    /// Gear-drive reading is pure: an anomaly is carried in the result, not logged
    /// </summary>
    public class GearDriveConversionTest
    {
        [Test]
        public void 歯車の詳細が無ければ電力駆動で異常は無い()
        {
            var reading = GearDriveConversion.Read((GearStateDetail)null, new object());

            Assert.IsFalse(reading.IsGearDriven);
            Assert.IsNull(reading.Anomaly);
        }

        [Test]
        public void 発電側は歯車駆動で異常は無い()
        {
            var reading = GearDriveConversion.Read(new GearStateDetail(true, 20f, 5f, GearRole.Generator), new object());

            Assert.IsTrue(reading.IsGearDriven);
            Assert.IsNull(reading.Anomaly);
        }

        [Test]
        public void 消費の値が無い消費側は電力扱いで異常を返しログは出さない()
        {
            var reading = GearDriveConversion.Read(new GearStateDetail(true, 5f, 2.4f, GearRole.Consumer), new object());

            Assert.IsFalse(reading.IsGearDriven);
            StringAssert.Contains("has no IGearConsumptionParam", reading.Anomaly);
        }
    }
}
