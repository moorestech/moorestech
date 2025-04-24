using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;

namespace Tests.CombinedTest.Game
{
    public class ChallengeTest
    {
        [Test]
        public void UnlockAllPreviousChallengeCompleteTest()
        {
            var (_, saveServiceProvider) = new MoorestechServerDIContainerGenerator().Create(TestModDirectory.ForUnitTestModDirectory);

            // TODO AllPreviousChallengeCompleteTest Targetがアンロックされていないことを確認
            
            // TODO AllPreviousChallengeCompleteTest1 をクリアする
            
            // TODO AllPreviousChallengeCompleteTest Targetがアンロックされていないことを確認
            
            // TODO AllPreviousChallengeCompleteTest2 をクリアする
            
            // TODO AllPreviousChallengeCompleteTest Targetがアンロックされていることを確認
            
            Assert.Fail();
            
        }
    }
}