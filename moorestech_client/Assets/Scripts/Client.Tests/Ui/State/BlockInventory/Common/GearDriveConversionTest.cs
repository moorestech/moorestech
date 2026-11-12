using Client.Ui.State.BlockInventory.Common;
using Game.Block.Interface.State;
using Game.Gear.Common;
using Mooresmaster.Model.BlocksModule;
using Mooresmaster.Model.GearConsumptionModule;
using NUnit.Framework;

namespace Client.Tests.Ui.State.BlockInventory.Common
{
    /// <summary>
    /// 歯車駆動の種類の読み口は純関数で、異常はログでなく結果に載る（族・機械・特殊族・Web ホストが共有する 1 本）
    /// Gear-drive reading is pure and the single reader shared by family, machine, special and the Web host; an anomaly is carried in the result, not logged
    /// </summary>
    public class GearDriveConversionTest
    {
        [Test]
        public void 歯車の詳細が無ければ電力駆動で異常は無い()
        {
            var reading = GearDriveConversion.Read((GearStateDetail)null, new object());

            Assert.AreEqual(GearDriveKind.Electric, reading.Kind);
            Assert.IsFalse(reading.IsGearDriven);
            Assert.IsNull(reading.Anomaly);
        }

        [Test]
        public void 発電側は消費のparamがあっても発電で歯車駆動()
        {
            var reading = GearDriveConversion.Read(new GearStateDetail(true, 20f, 5f, GearRole.Generator), new GearConsumptionParamStub());

            Assert.AreEqual(GearDriveKind.Generator, reading.Kind);
            Assert.IsTrue(reading.IsGearDriven);
            Assert.IsNull(reading.ConsumptionParam);
            Assert.IsNull(reading.Anomaly);
        }

        [Test]
        public void 消費のparamがある消費側はその値を持って歯車駆動()
        {
            var param = new GearConsumptionParamStub();
            var reading = GearDriveConversion.Read(new GearStateDetail(true, 5f, 2.4f, GearRole.Consumer), param);

            Assert.AreEqual(GearDriveKind.Consumer, reading.Kind);
            Assert.IsTrue(reading.IsGearDriven);
            Assert.AreSame(param, reading.ConsumptionParam);
            Assert.IsNull(reading.Anomaly);
        }

        [Test]
        public void 消費の値が無い消費側は電力扱いで異常を返しログは出さない()
        {
            var reading = GearDriveConversion.Read(new GearStateDetail(true, 5f, 2.4f, GearRole.Consumer), new object());

            Assert.AreEqual(GearDriveKind.InconsistentConsumer, reading.Kind);
            Assert.IsFalse(reading.IsGearDriven);
            Assert.IsNull(reading.ConsumptionParam);
            StringAssert.Contains("has no IGearConsumptionParam", reading.Anomaly);
        }

        private sealed class GearConsumptionParamStub : IGearConsumptionParam
        {
            public GearConsumption GearConsumption { get; } = new(7f, 0f, 0.1f, 0.2f, 2f, 1.585f);
        }
    }
}
