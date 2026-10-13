using Client.WebUiHost.Game.Topics.BlockDetail;
using Core.Master;
using Game.Block.Blocks.GearToElectric;
using Game.EnergySystem;
using Game.Gear.Common;
using Mooresmaster.Model.BlocksModule;
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;

namespace Client.Tests.WebUi.BlockDetail
{
    public class GearToElectricDetailDtoBuilderTest
    {
        // サーバーの3状態を文字列にし、最大値はマスタから読む
        // Serialize all three server states and read the maximum from master
        [TestCase(GearToElectricGenerationState.Generating, "generating")]
        [TestCase(GearToElectricGenerationState.Idle, "idle")]
        [TestCase(GearToElectricGenerationState.Stopped, "stopped")]
        public void BuildDetailCopiesStateAndMasterMaximum(GearToElectricGenerationState state, string expectedState)
        {
            new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var param = (GearToElectricGeneratorBlockParam)MasterHolder.BlockMaster.GetBlockMaster(ForUnitTestModBlockId.TestGearToElectricGenerator).BlockParam;
            var detail = new GearToElectricGeneratorBlockStateDetail(true, new RPM(120f), new Torque(100f), new ElectricPower(72f), state);
            var dto = GearToElectricDetailDtoBuilder.BuildDetail(detail, param);
            Assert.AreEqual(expectedState, dto.GenerationState);
            Assert.AreEqual(72f, dto.SuppliedPower, 0.001f);
            Assert.AreEqual(100f, dto.MaxGeneratedPower, 0.001f);
        }
    }
}
