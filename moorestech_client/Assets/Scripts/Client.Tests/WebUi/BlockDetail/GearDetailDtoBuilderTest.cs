using Client.WebUiHost.Game.Topics.BlockDetail;
using Core.Master;
using Game.Gear.Common;
using Mooresmaster.Model.BlocksModule;
using Mooresmaster.Model.GearConsumptionModule;
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;

namespace Client.Tests.WebUi.BlockDetail
{
    /// <summary>
    /// 歯車の役割と基準RPMがスキーマの IGearConsumptionParam だけで決まることを固定する
    /// Pins that the gear's role and base RPM are settled solely by the schema's IGearConsumptionParam
    /// </summary>
    public class GearDetailDtoBuilderTest
    {
        // 実マスタの発電機ブロック。gearConsumptionを持たないので発電機かつ基準RPM無し
        // The real generator block from master: it has no gearConsumption, so it is a generator with no base RPM
        [Test]
        public void BuildGearDetailOmitsBaseRpmForMasterGeneratorBlock()
        {
            new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));

            var param = MasterHolder.BlockMaster.GetBlockMaster(ForUnitTestModBlockId.SimpleGearGenerator).BlockParam;
            var gearDto = GearDetailDtoBuilder.BuildGearDetail(new GearStateDetail(true, 20f, 5f), param);

            Assert.AreEqual("generator", gearDto.Role);
            Assert.IsNull(gearDto.BaseRpm);
            Assert.AreEqual(20f, gearDto.CurrentRpm, 0.001f);
            Assert.AreEqual(5f, gearDto.CurrentTorque, 0.001f);
        }

        // 実マスタの歯車機械。gearConsumptionを持つので消費側かつ基準RPMはマスタ値そのもの
        // The real gear machine from master: it has gearConsumption, so it is a consumer whose base RPM is the master value
        [Test]
        public void BuildGearDetailCarriesMasterBaseRpmForMasterConsumerBlock()
        {
            new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));

            var master = MasterHolder.BlockMaster.GetBlockMaster(ForUnitTestModBlockId.GearMachine);
            var expectedBaseRpm = (float)((IGearConsumptionParam)master.BlockParam).GearConsumption.BaseRpm;
            var gearDto = GearDetailDtoBuilder.BuildGearDetail(new GearStateDetail(true, 12.5f, 3f), master.BlockParam);

            Assert.AreEqual("consumer", gearDto.Role);
            Assert.AreEqual(10f, expectedBaseRpm, 0.001f);
            Assert.AreEqual(expectedBaseRpm, gearDto.BaseRpm.Value, 0.001f);
            Assert.AreEqual(12.5f, gearDto.CurrentRpm, 0.001f);
            Assert.AreEqual(3f, gearDto.CurrentTorque, 0.001f);
        }

        // 消費パラメータを持つブロックは消費側で、基準RPMはパラメータから来る
        // A block carrying the consumption param is a consumer, and its base RPM comes from that param
        [Test]
        public void BuildGearDetailReturnsConsumerForConsumptionParam()
        {
            var gearDto = GearDetailDtoBuilder.BuildGearDetail(new GearStateDetail(true, 1f, 2f), new GearConsumptionParamStub());

            Assert.AreEqual("consumer", gearDto.Role);
            Assert.AreEqual(7f, gearDto.BaseRpm.Value, 0.001f);
        }

        // 消費パラメータを持たないブロックは発電機で、基準RPMのキーはwireから落ちる
        // A block without the consumption param is a generator, and the base RPM key drops off the wire
        [Test]
        public void BuildGearDetailReturnsGeneratorForParamWithoutConsumption()
        {
            var gearDto = GearDetailDtoBuilder.BuildGearDetail(new GearStateDetail(true, 1f, 2f), new GearGeneratorParamStub());

            Assert.AreEqual("generator", gearDto.Role);
            Assert.IsNull(gearDto.BaseRpm);
        }

        private class GearConsumptionParamStub : IGearConsumptionParam
        {
            // 引数順は baseRpm, minimumRpm, baseTorque, idlePowerRate, torqueExponentUnder, torqueExponentOver
            // Argument order: baseRpm, minimumRpm, baseTorque, idlePowerRate, torqueExponentUnder, torqueExponentOver
            public GearConsumption GearConsumption { get; } = new(7f, 0f, 0.1f, 0.2f, 2f, 1.585f);
        }

        private class GearGeneratorParamStub
        {
        }
    }
}
