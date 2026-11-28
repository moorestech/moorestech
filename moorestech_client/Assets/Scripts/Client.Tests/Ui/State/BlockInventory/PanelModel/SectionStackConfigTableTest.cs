using System;
using Client.Ui.State.BlockInventory.SectionStack;
using Mooresmaster.Model.BlocksModule;
using NUnit.Framework;

namespace Client.Tests.Ui.State.BlockInventory.PanelModel
{
    /// <summary>
    /// ブロック画面IDに対応する標準本文の設定を確かめる（L0）
    /// Checks the standard-body settings keyed by block screen ID (L0)
    /// </summary>
    public class SectionStackConfigTableTest
    {
        [Test]
        public void Chestは空でも出す項目グリッドで液体は出さない()
        {
            var config = SectionStackConfigTable.Resolve(BlockMasterElement.BlockScreenConst.Chest);
            var grid = (ShownSectionStackItemGrid)config.ItemGrid;
            Assert.AreEqual("chest-grid", grid.GridId);
            Assert.IsTrue(grid.RenderEmpty);
            Assert.AreSame(SectionStackFluid.Hidden, config.Fluid);
        }

        [Test]
        public void Genericは空なら隠し液体の行には進捗を出す()
        {
            var config = SectionStackConfigTable.Resolve(BlockMasterElement.BlockScreenConst.Generic);
            var grid = (ShownSectionStackItemGrid)config.ItemGrid;
            Assert.AreEqual("generic-block-grid", grid.GridId);
            Assert.IsFalse(grid.RenderEmpty);
            var fluid = (RowSectionStackFluid)config.Fluid;
            Assert.AreEqual("generic-block-fluids", fluid.RowId);
            Assert.IsTrue(fluid.ShowProgress);
        }

        [Test]
        public void 未登録の画面IDは例外になる()
        {
            Assert.Throws<InvalidOperationException>(() => SectionStackConfigTable.Resolve("Unknown"));
        }

        [Test]
        public void FluidPipeは項目グリッドを出さず液体は詳細()
        {
            var config = SectionStackConfigTable.Resolve(BlockMasterElement.BlockScreenConst.FluidPipe);
            Assert.AreSame(SectionStackItemGrid.Hidden, config.ItemGrid);
            Assert.AreEqual("fluid-pipe-fluid-slots", ((DetailsSectionStackFluid)config.Fluid).RowId);
        }
    }
}
