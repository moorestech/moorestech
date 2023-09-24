using Game.Block;
using Game.Block.Blocks;
using Core.Const;
using Game.Block.Interface;
using NUnit.Framework;

namespace Test.UnitTest.Game.Block
{
    public class NullBlockTest
    {
        [Test]
        public void Test()
        {
            var block = new NullBlock();
            Assert.AreEqual(int.MaxValue, block.EntityId);
            Assert.AreEqual(BlockConst.EmptyBlockId, block.BlockId);
        }
    }
}