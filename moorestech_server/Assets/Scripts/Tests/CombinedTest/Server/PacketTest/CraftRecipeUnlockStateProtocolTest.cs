using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;

namespace Tests.CombinedTest.Server.PacketTest
{
    public class CraftRecipeUnlockStateProtocolTest
    {
        [Test]
        public void GetRecipeStateInfo()
        {
            var (packet, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(TestModDirectory.ForUnitTestModDirectory);
            
            // TODO レシピのアンロック状態を取得
            
            // TODO assert
            
            // TODO レシピのアンロック状態を変更
            
            // TODO assert
            
            Assert.Fail();
        }
            
    }
}