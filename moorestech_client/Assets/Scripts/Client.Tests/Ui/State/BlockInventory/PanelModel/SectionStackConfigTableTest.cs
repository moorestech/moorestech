using Client.Ui.State.BlockInventory.SectionStack;
using Mooresmaster.Model.BlocksModule;
using NUnit.Framework;

namespace Client.Tests.Ui.State.BlockInventory.PanelModel
{
    /// <summary>
    /// Web SectionStackView の configByBlockType と genericConfig の写しを主張する（L0）
    /// Asserts the port of Web SectionStackView configByBlockType and genericConfig (L0)
    /// </summary>
    public class SectionStackConfigTableTest
    {
        [Test]
        public void Chestは空でも出す項目グリッドで液体は出さない()
        {
            var config = SectionStackConfigTable.Resolve(BlockMasterElement.BlockTypeConst.Chest);
            var grid = (ShownSectionStackItemGrid)config.ItemGrid;
            Assert.AreEqual("chest-grid", grid.GridId);
            Assert.IsTrue(grid.RenderEmpty);
            Assert.AreSame(SectionStackFluid.Hidden, config.Fluid);
        }

        [Test]
        public void 未知の種類は標準の設定()
        {
            var config = SectionStackConfigTable.Resolve("Unknown");
            var grid = (ShownSectionStackItemGrid)config.ItemGrid;
            Assert.AreEqual("generic-block-grid", grid.GridId);
            Assert.IsFalse(grid.RenderEmpty);
            var fluid = (RowSectionStackFluid)config.Fluid;
            Assert.AreEqual("generic-block-fluids", fluid.RowId);
            Assert.IsTrue(fluid.ShowProgress);
        }

        [Test]
        public void FluidPipeは項目グリッドを出さず液体は詳細()
        {
            var config = SectionStackConfigTable.Resolve(BlockMasterElement.BlockTypeConst.FluidPipe);
            Assert.AreSame(SectionStackItemGrid.Hidden, config.ItemGrid);
            Assert.AreEqual("fluid-pipe-fluid-slots", ((DetailsSectionStackFluid)config.Fluid).RowId);
        }
    }
}
