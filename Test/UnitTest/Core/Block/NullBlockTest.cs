using Core.Block;
using Core.Block.Blocks;
using Core.Const;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Test.UnitTest.Core.Block
{
    [TestClass]
    public class NullBlockTest
    {
        [TestMethod]
        public void Test()
        {
            var block = new NullBlock();
            Assert.AreEqual(int.MaxValue, block.GetIntId());
            Assert.AreEqual(BlockConst.EmptyBlockId, block.GetBlockId());
        }
    }
}