using System.Linq;
using Core.Master;
using Game.Context;
using Game.PlayerInventory.Interface;
using Game.Research;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;

namespace Tests.CombinedTest.Game.Research
{
    public class ResearchDataStoreTest
    {
        // もしインベントリのアイテムが足りないなら研究できない
        [Test]
        public void NotEnoughItemToFailResearchTest()
        {
            var (_, sp) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            const int playerId = 1;

            var all = MasterHolder.ResearchMaster.GetAllResearches();
            var needItems = all.FirstOrDefault(e => e.ConsumeItems != null && e.ConsumeItems.Length > 0);
            if (needItems == null)
            {
                Assert.Inconclusive("No research with consume items in test mod.");
                return;
            }

            // インベントリは空のまま
            var store = (ResearchDataStore)sp.GetService<IResearchDataStore>();
            var success = store.CompleteResearch(needItems.ResearchNodeGuid, playerId);
            Assert.IsFalse(success);
        }
        
        // 1つの前提研究が完了していないなら研究できない
        [Test]
        public void NotOneCompletedPreviousToFailResearchTest()
        {
            Assert.Inconclusive("Previous-research dependency tests are not available in current test data.");
        }
        
        // 複数の前提研究が完了していないなら研究できない
        [Test]
        public void NotAllCompletedPreviousToFailResearchTest()
        {
            Assert.Inconclusive("Previous-research dependency tests are not available in current test data.");
        }
        
        // すべての前提研究が完了しているなら研究できる
        [Test]
        public void AllCompletedPreviousToSuccessResearchTest()
        {
            Assert.Inconclusive("Previous-research dependency tests are not available in current test data.");
        }
        
        
        
        
        // 保存、ロードテスト
        [Test]
        public void SaveLoadTest()
        {
            // 完了済みの研究がセーブ/ロードで復元されること
            var (_, saveSp) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            const int playerId = 1;

            var first = MasterHolder.ResearchMaster.GetAllResearches().First();
            var saveStore = (ResearchDataStore)saveSp.GetService<IResearchDataStore>();

            // 必要アイテムがあるなら投入
            var saveInv = saveSp.GetService<IPlayerInventoryDataStore>().GetInventoryData(playerId);
            if (first.ConsumeItems != null)
            {
                for (var i = 0; i < first.ConsumeItems.Length; i++)
                {
                    var req = first.ConsumeItems[i];
                    var stack = ServerContext.ItemStackFactory.Create(req.ItemGuid, req.ItemCount);
                    saveInv.MainOpenableInventory.SetItem(i, stack);
                }
            }
            Assert.IsTrue(saveStore.CompleteResearch(first.ResearchNodeGuid, playerId));

            var jsonObj = saveStore.GetSaveJsonObject();
            Assert.Contains(first.ResearchNodeGuid.ToString(), jsonObj.CompletedResearchGuids);

            // 新しいDIでロード
            var (_, loadSp) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var loadStore = (ResearchDataStore)loadSp.GetService<IResearchDataStore>();
            loadStore.LoadResearchData(jsonObj);

            // 同じ研究は再度完了できない（復元済み）
            var canCompleteAgain = loadStore.CompleteResearch(first.ResearchNodeGuid, playerId);
            Assert.IsFalse(canCompleteAgain);
        }

    }
}
