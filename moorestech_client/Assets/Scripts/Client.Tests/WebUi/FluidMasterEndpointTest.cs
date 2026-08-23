using System;
using System.Linq;
using Client.WebUiHost.Common;
using Client.WebUiHost.Game;
using Core.Master;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;

namespace Client.Tests.WebUi
{
    public class FluidMasterEndpointTest
    {
        private static readonly Guid TestWaterFluidGuid = Guid.Parse("00000000-0000-0000-1234-000000000001");

        [Test]
        public void BuildResponseServesGuidAndColorOfEveryMasterFluid()
        {
            new MoorestechServerDIContainerGenerator().Create(
                new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));

            // マスタ定義の色をそのまま配信する
            // Serve the master-defined color verbatim
            var response = FluidMasterEndpoint.BuildResponse();
            var water = response.Fluids.Single(fluid => fluid.FluidGuid == TestWaterFluidGuid.ToString("D"));

            Assert.AreEqual("#3399FF", water.Color);
        }

        [Test]
        public void BuildResponseExcludesReservedMixedFluid()
        {
            new MoorestechServerDIContainerGenerator().Create(
                new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));

            // 予約MixedFluidはUI表示対象外であり、guidも境界の厳格な検証を通せないため配信しない
            // The reserved MixedFluid is never shown in the UI and its guid cannot pass the strict boundary check, so it is not served
            var response = FluidMasterEndpoint.BuildResponse();
            var reservedGuidText = FluidMaster.MixedFluidGuid.ToString("D");

            Assert.IsTrue(MasterHolder.FluidMaster.GetAllFluidIds()
                .Any(fluidId => MasterHolder.FluidMaster.GetFluidMaster(fluidId).FluidGuid == FluidMaster.MixedFluidGuid));
            CollectionAssert.DoesNotContain(response.Fluids.Select(fluid => fluid.FluidGuid), reservedGuidText);
        }

        [Test]
        public void BuildResponseSerializesFluidGuidAndColorOnly()
        {
            new MoorestechServerDIContainerGenerator().Create(
                new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));

            // 揮発FluidIdは公開契約に含めない
            // The volatile FluidId is not part of the public contract
            var wire = JToken.Parse(WebUiJson.Serialize(FluidMasterEndpoint.BuildResponse()));
            var fluid = wire["fluids"]!.Single(entry => (string)entry["fluidGuid"]! == TestWaterFluidGuid.ToString("D"));

            CollectionAssert.AreEquivalent(
                new[] { "fluidGuid", "color" },
                ((JObject)fluid).Properties().Select(property => property.Name));
        }
    }
}
