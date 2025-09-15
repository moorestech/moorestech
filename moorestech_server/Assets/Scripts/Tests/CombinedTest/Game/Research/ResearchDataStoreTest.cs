using NUnit.Framework;

namespace Tests.CombinedTest.Game.Research
{
    public class ResearchDataStoreTest
    {
        // もしインベントリのアイテムが足りないなら研究できない
        [Test]
        public void NotEnoughItemToFailResearchTest()
        {
        }
        
        // 1つの前提研究が完了していないなら研究できない
        [Test]
        public void NotOneCompletedPreviousToFailResearchTest()
        {
            
        }
        
        // 複数の前提研究が完了していないなら研究できない
        [Test]
        public void NotAllCompletedPreviousToFailResearchTest()
        {
            
        }
        
        // すべての前提研究が完了しているなら研究できる
        [Test]
        public void AllCompletedPreviousToSuccessResearchTest()
        {
            
        }
        
        
        
        
        // 保存、ロードテスト
        [Test]
        public void SaveLoadTest()
        {
        }

    }
}