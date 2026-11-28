using System;
using Client.Ui.State.BlockInventory.Logic.Panel;
using NUnit.Framework;
using S = Mooresmaster.Model.BlocksModule.BlockMasterElement.BlockScreenConst;

namespace Client.Tests.Ui.State.BlockInventory.Panel
{
    public class BlockComponentRegistryTest
    {
        [TestCase(S.Generic)]
        [TestCase(S.Chest)]
        [TestCase(S.Machine)]
        [TestCase(S.GearMiner)]
        public void 標準の本文は題の操作を持たない(string blockScreen)
        {
            var components = BlockComponentRegistry.Resolve(blockScreen);
            Assert.AreEqual(BlockInventoryBodyKind.SectionStack, components.Body);
            Assert.AreEqual(BlockInventoryTitleActionKind.None, components.TitleAction);
        }

        [TestCase("chest")]
        [TestCase("tank")]
        [TestCase("unknown")]
        [TestCase("GearEnergyTransformer")]
        public void 未登録の画面IDは例外になる(string blockScreen)
        {
            var error = Assert.Throws<InvalidOperationException>(() => BlockComponentRegistry.Resolve(blockScreen));
            StringAssert.Contains(blockScreen, error.Message);
        }

        [TestCase(S.FilterSplitter, BlockInventoryBodyKind.FilterSplitter)]
        [TestCase(S.ElectricToGearGenerator, BlockInventoryBodyKind.ElectricToGear)]
        [TestCase(S.TrainPlatform, BlockInventoryBodyKind.TrainPlatform)]
        [TestCase(S.ElectricPole, BlockInventoryBodyKind.ElectricPole)]
        [TestCase(S.SpaceElevatorBase, BlockInventoryBodyKind.SpaceElevator)]
        [TestCase(S.RocketLaunchPad, BlockInventoryBodyKind.RocketLaunchPad)]
        public void 専用画面IDは対応する本文を引く(string blockScreen, BlockInventoryBodyKind expectedBody)
        {
            Assert.AreEqual(expectedBody, BlockComponentRegistry.Resolve(blockScreen).Body);
        }
    }
}
