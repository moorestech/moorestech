using System;
using System.Collections.Generic;
using Client.Game.InGame.BlockSystem.PlaceSystem.Util;
using Core.Master;
using Game.Context;
using Mooresmaster.Model.BlocksModule;
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;

// namespaceは既存の隣接テスト（CommonBlockPlacePointCalculatorTest等）に合わせること
// Match the namespace of sibling tests such as CommonBlockPlacePointCalculatorTest
namespace Client.Tests.PlaceSystem
{
    public class ConstructionCostPreviewCalculatorTest
    {
        private static readonly Guid Material1Guid = Guid.Parse("00000000-0000-0000-1234-000000000003"); // Test3(コスト×2)
        private static readonly Guid Material2Guid = Guid.Parse("00000000-0000-0000-1234-000000000004"); // Test4(コスト×1)

        [Test]
        public void 素材所持数から設置可能なエンティティ数を算出する()
        {
            CreateServer();
            var entityCosts = BuildUniformCosts(ForUnitTestModBlockId.BlockId, 3);
            var factory = ServerContext.ItemStackFactory;

            // Test3=5個(2セル半) Test4=2個(2セル) → 賄えるのは2セル
            // Test3=5 (2.5 cells) and Test4=2 (2 cells) afford exactly 2 cells
            var inventory = new List<global::Core.Item.Interface.IItemStack>
            {
                factory.Create(MasterHolder.ItemMaster.GetItemId(Material1Guid), 5),
                factory.Create(MasterHolder.ItemMaster.GetItemId(Material2Guid), 2),
            };

            Assert.AreEqual(2, ConstructionCostPreviewCalculator.CalculateAffordableEntityCount(entityCosts, inventory));
        }

        [Test]
        public void コスト未定義なら全エンティティを賄える()
        {
            CreateServer();
            var entityCosts = BuildUniformCosts(ForUnitTestModBlockId.BeltConveyorId, 3);

            Assert.AreEqual(3, ConstructionCostPreviewCalculator.CalculateAffordableEntityCount(entityCosts, new List<global::Core.Item.Interface.IItemStack>()));
        }

        [Test]
        public void 素材が1種でも足りなければ0エンティティ()
        {
            CreateServer();
            var entityCosts = BuildUniformCosts(ForUnitTestModBlockId.BlockId, 3);
            var factory = ServerContext.ItemStackFactory;

            // Test4を持っていないため0セル
            // Zero cells because no Test4 is held
            var inventory = new List<global::Core.Item.Interface.IItemStack>
            {
                factory.Create(MasterHolder.ItemMaster.GetItemId(Material1Guid), 10),
            };

            Assert.AreEqual(0, ConstructionCostPreviewCalculator.CalculateAffordableEntityCount(entityCosts, inventory));
        }

        [Test]
        public void 先に賄えないエンティティが出たら以降は数えない()
        {
            CreateServer();
            var factory = ServerContext.ItemStackFactory;

            // 2番目が賄えない時点で打ち切るため、3番目のコスト無しブロックは数えない
            // Counting stops at the second unaffordable entity, so the costless third block is not counted
            var entityCosts = new List<ConstructionRequiredItemElement[]>
            {
                MasterHolder.BlockMaster.GetBlockMaster(ForUnitTestModBlockId.BlockId).RequiredItems,
                MasterHolder.BlockMaster.GetBlockMaster(ForUnitTestModBlockId.BlockId).RequiredItems,
                MasterHolder.BlockMaster.GetBlockMaster(ForUnitTestModBlockId.BeltConveyorId).RequiredItems,
            };
            var inventory = new List<global::Core.Item.Interface.IItemStack>
            {
                factory.Create(MasterHolder.ItemMaster.GetItemId(Material1Guid), 3),
                factory.Create(MasterHolder.ItemMaster.GetItemId(Material2Guid), 10),
            };

            Assert.AreEqual(1, ConstructionCostPreviewCalculator.CalculateAffordableEntityCount(entityCosts, inventory));
        }

        private static List<ConstructionRequiredItemElement[]> BuildUniformCosts(BlockId blockId, int entityCount)
        {
            var requiredItems = MasterHolder.BlockMaster.GetBlockMaster(blockId).RequiredItems;
            var entityCosts = new List<ConstructionRequiredItemElement[]>(entityCount);
            for (var i = 0; i < entityCount; i++) entityCosts.Add(requiredItems);
            return entityCosts;
        }

        private static void CreateServer()
        {
            new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
        }
    }
}
