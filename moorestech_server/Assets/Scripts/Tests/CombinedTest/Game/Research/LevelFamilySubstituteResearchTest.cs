using System.Linq;
using Newtonsoft.Json.Linq;
using Core.Inventory;
using Core.Item.Interface;
using Core.Master;
using Game.Context;
using Game.PlayerIdentity;
using Game.PlayerInventory.Interface;
using Game.Research;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;
using Tests.Util.PlayerIdentity;

namespace Tests.CombinedTest.Game.Research
{
    /// <summary>
    ///     研究の基準要求に上位の変種を数え、消費は下位から行い、他ファミリーは数えないことを確かめる
    ///     Verifies research counts higher variants toward a base requirement, consumes the lowest level first, and ignores other families
    /// </summary>
    public class LevelFamilySubstituteResearchTest
    {
        [Test]
        public void HigherLevelSatisfiesBaseRequirementTest()
        {
            var (researchDataStore, inventory, playerId) = Setup();
            inventory.InsertItem(Create(ForUnitTestLevelInheritanceIds.Source(2), 3));

            Assert.AreEqual(ResearchNodeState.Researchable, researchDataStore.GetResearchNodeStates(playerId)[ForUnitTestLevelInheritanceIds.SubstituteResearchGuid]);
            Assert.IsTrue(researchDataStore.CompleteResearch(ForUnitTestLevelInheritanceIds.SubstituteResearchGuid, playerId));
            Assert.AreEqual(0, Count(inventory, ForUnitTestLevelInheritanceIds.Source(2)));
        }

        [Test]
        public void LowerLevelIsConsumedFirstTest()
        {
            var (researchDataStore, inventory, playerId) = Setup();

            // Lv3 を先のスロットへ入れても、スロット順でなくレベル順に Lv1 から減る
            // Even with Lv3 in the earlier slots, consumption follows level order from Lv1, not slot order
            inventory.InsertItem(Create(ForUnitTestLevelInheritanceIds.Source(3), 5));
            inventory.InsertItem(Create(ForUnitTestLevelInheritanceIds.Source(1), 2));
            Assert.IsTrue(researchDataStore.CompleteResearch(ForUnitTestLevelInheritanceIds.SubstituteResearchGuid, playerId));

            Assert.AreEqual(0, Count(inventory, ForUnitTestLevelInheritanceIds.Source(1)));
            Assert.AreEqual(4, Count(inventory, ForUnitTestLevelInheritanceIds.Source(3)));
        }

        [Test]
        public void ShortageCountsEveryLevelTest()
        {
            var (researchDataStore, inventory, playerId) = Setup();

            // Lv1×1＋Lv2×1 は合わせて2個で、3個には足りない
            // Lv1×1 plus Lv2×1 counts as two, which is short of three
            inventory.InsertItem(Create(ForUnitTestLevelInheritanceIds.Source(1), 1));
            inventory.InsertItem(Create(ForUnitTestLevelInheritanceIds.Source(2), 1));
            Assert.AreEqual(ResearchNodeState.UnresearchableNotEnoughItem, researchDataStore.GetResearchNodeStates(playerId)[ForUnitTestLevelInheritanceIds.SubstituteResearchGuid]);
            Assert.IsFalse(researchDataStore.CompleteResearch(ForUnitTestLevelInheritanceIds.SubstituteResearchGuid, playerId));
            Assert.AreEqual(1, Count(inventory, ForUnitTestLevelInheritanceIds.Source(1)));

            // Lv3 を1個足すと3個になり研究できる
            // Adding one Lv3 makes three and the research becomes available
            inventory.InsertItem(Create(ForUnitTestLevelInheritanceIds.Source(3), 1));
            Assert.AreEqual(ResearchNodeState.Researchable, researchDataStore.GetResearchNodeStates(playerId)[ForUnitTestLevelInheritanceIds.SubstituteResearchGuid]);
        }

        [Test]
        public void OtherFamilyDoesNotCountTest()
        {
            var (researchDataStore, inventory, playerId) = Setup();
            inventory.InsertItem(Create(ForUnitTestLevelInheritanceIds.Target(2), 3));

            Assert.AreEqual(ResearchNodeState.UnresearchableNotEnoughItem, researchDataStore.GetResearchNodeStates(playerId)[ForUnitTestLevelInheritanceIds.SubstituteResearchGuid]);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void OverlappingRequirementsReserveExactVariantTest(bool reverse)
        {
            var (research, inventory, playerId) = Setup();
            var container = ServerContext.GetService<MasterJsonFileContainer>();
            var key = new JsonFileName("research");
            var config = container.ConfigJsons.Single(entry => entry.JsonContents.ContainsKey(key));
            var json = JObject.Parse(config.JsonContents[key]);
            var node = json["data"].Single(value => (string)value["researchNodeGuid"] == ForUnitTestLevelInheritanceIds.SubstituteResearchGuid.ToString());
            var baseRequirement = new JObject { ["itemGuid"] = MasterHolder.ItemMaster.GetItemGuid(ForUnitTestLevelInheritanceIds.Source(1)).ToString(), ["itemCount"] = 1 };
            var exactRequirement = new JObject { ["itemGuid"] = MasterHolder.ItemMaster.GetItemGuid(ForUnitTestLevelInheritanceIds.Source(2)).ToString(), ["itemCount"] = 1 };
            node["consumeItems"] = reverse ? new JArray(exactRequirement, baseRequirement) : new JArray(baseRequirement, exactRequirement);
            config.JsonContents[key] = json.ToString();
            MasterHolder.Load(container);

            // 同じ1個を二重に割り当てず、不足時は在庫を保つ
            // Never allocate one item twice; preserve inventory on shortage
            inventory.InsertItem(Create(ForUnitTestLevelInheritanceIds.Source(2), 1));
            Assert.IsFalse(research.CompleteResearch(ForUnitTestLevelInheritanceIds.SubstituteResearchGuid, playerId));
            Assert.AreEqual(1, Count(inventory, ForUnitTestLevelInheritanceIds.Source(2)));
            inventory.InsertItem(Create(ForUnitTestLevelInheritanceIds.Source(3), 1));
            Assert.IsTrue(research.CompleteResearch(ForUnitTestLevelInheritanceIds.SubstituteResearchGuid, playerId));
            Assert.AreEqual(0, Count(inventory, ForUnitTestLevelInheritanceIds.Source(2)));
            Assert.AreEqual(0, Count(inventory, ForUnitTestLevelInheritanceIds.Source(3)));
        }

        private static (IResearchDataStore researchDataStore, IOpenableInventory inventory, int playerId) Setup()
        {
            var (_, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var playerId = PlayerIdentityTestHelper.Register(serviceProvider.GetRequiredService<PlayerIdentityRegistry>(), "steam:1").PlayerId;
            var inventory = serviceProvider.GetService<IPlayerInventoryDataStore>().GetInventoryData(playerId).MainOpenableInventory;
            return (serviceProvider.GetService<IResearchDataStore>(), inventory, playerId);
        }

        private static IItemStack Create(ItemId itemId, int count)
        {
            return ServerContext.ItemStackFactory.Create(itemId, count);
        }

        private static int Count(IOpenableInventory inventory, ItemId itemId)
        {
            var total = 0;
            foreach (var stack in inventory.InventoryItems)
            {
                if (stack.Id == itemId) total += stack.Count;
            }
            return total;
        }
    }
}
