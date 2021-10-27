using Core.Block;
using NUnit.Framework;

namespace Test.UnitTest.Core.Block
{
    public class NullBlockTest
    {
        [Test]
        public void Test()
        {
            var block = new NullBlock(0,100);
            Assert.AreEqual(int.MaxValue,block.GetIntId());
            Assert.AreEqual(BlockConst.NullBlockId,block.GetBlockId());
        }
        
    }
}