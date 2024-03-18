using Core.Const;
using Game.Block.Base;
using Game.Block.Interface;
using NUnit.Framework;

namespace Tests.UnitTest.Core.Block
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