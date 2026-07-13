using NUnit.Framework;
using MapGenerator.Pipeline.Spawn;

namespace MapGenerator.Tests.EditMode.Spawn
{
    public class ConnectedComponentsTests
    {
        // グリッド: 1=対象, 0=非対象。3x3で中央十字を1つの成分として検出
        [Test]
        public void Label_SingleCrossComponent_AreaIs5()
        {
            // 0 1 0
            // 1 1 1
            // 0 1 0
            int[] grid = { 0,1,0, 1,1,1, 0,1,0 };
            var comps = ConnectedComponents.Label(grid, 3, 3, v => v == 1);
            Assert.AreEqual(1, comps.Count);
            Assert.AreEqual(5, comps[0].Area);
        }

        [Test]
        public void Label_TwoSeparateComponents_AreSeparate()
        {
            // 1 0 1
            // 1 0 1
            int[] grid = { 1,0,1, 1,0,1 };
            var comps = ConnectedComponents.Label(grid, 3, 2, v => v == 1);
            Assert.AreEqual(2, comps.Count);
            Assert.AreEqual(2, comps[0].Area);
            Assert.AreEqual(2, comps[1].Area);
        }

        [Test]
        public void Label_DiagonalCells_AreNotConnected()
        {
            // 1 0
            // 0 1  → 4近傍では対角は非連結 → 2成分
            int[] grid = { 1,0, 0,1 };
            var comps = ConnectedComponents.Label(grid, 2, 2, v => v == 1);
            Assert.AreEqual(2, comps.Count);
        }

        [Test]
        public void BorderContact_ConcaveACell_CountsContactingCellsNotEdges()
        {
            // A(1) single corner cell; B(2) fills the other 3 (one 4-connected component).
            // 1 2
            // 2 2
            int[] grid = { 1,2, 2,2 };
            var a = ConnectedComponents.Label(grid, 2, 2, v => v == 1);
            var b = ConnectedComponents.Label(grid, 2, 2, v => v == 2);
            // A touches B on 2 sides (right + down) but is counted ONCE (cell-count semantics).
            int contact = ConnectedComponents.BorderContactCells(a[0], b[0], grid, 2, 2);
            Assert.AreEqual(1, contact);
        }

        [Test]
        public void BorderContact_AdjacentComponents_CountsSharedEdges()
        {
            // 左列=A(値1), 右列=B(値2), 中央列=0 ではなく直接隣接させる
            // 1 2
            // 1 2
            int[] grid = { 1,2, 1,2 };
            var aComps = ConnectedComponents.Label(grid, 2, 2, v => v == 1);
            var bComps = ConnectedComponents.Label(grid, 2, 2, v => v == 2);
            int contact = ConnectedComponents.BorderContactCells(
                aComps[0], bComps[0], grid, 2, 2);
            // A列(x=0)とB列(x=1)が縦2セル分隣接 → 2
            Assert.AreEqual(2, contact);
        }
    }
}
