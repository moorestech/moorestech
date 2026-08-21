using System;
using System.Collections.Generic;
using Client.Game.InGame.BlockSystem.PlaceSystem.Util;
using Core.Master;
using Game.Context;
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;

namespace Client.Tests.PlaceSystem.Util
{
    public class ConstructionCostShortageCalculatorTest
    {
        private static readonly Guid Material1Guid = Guid.Parse("00000000-0000-0000-1234-000000000003"); // Test3(コスト×2)
        private static readonly Guid Material2Guid = Guid.Parse("00000000-0000-0000-1234-000000000004"); // Test4(コスト×1)

        [Test]
        public void 不足素材のみ所持数と全セル分の必要数で返す()
        {
            CreateServer();
            var requiredItems = MasterHolder.BlockMaster.GetBlockMaster(ForUnitTestModBlockId.BlockId).RequiredItems;
            var material1Id = MasterHolder.ItemMaster.GetItemId(Material1Guid);
            var material2Id = MasterHolder.ItemMaster.GetItemId(Material2Guid);
            var inventory = new List<global::Core.Item.Interface.IItemStack>
            {
                ServerContext.ItemStackFactory.Create(material1Id, 3),
                ServerContext.ItemStackFactory.Create(material2Id, 10),
            };

            // 5セル: Material1 は 2×5=10 必要で所持3、Material2 は 1×5=5 必要で所持10（足りている）
            // 5 cells: Material1 needs 2x5=10 with 3 held; Material2 needs 1x5=5 with 10 held (enough)
            var shortages = ConstructionCostShortageCalculator.Calculate(requiredItems, 5, inventory);

            Assert.AreEqual(1, shortages.Count);
            Assert.AreEqual(material1Id, shortages[0].ItemId);
            Assert.AreEqual(3, shortages[0].Held);
            Assert.AreEqual(10, shortages[0].Required);
        }

        [Test]
        public void 全素材が足りていれば空を返す()
        {
            CreateServer();
            var requiredItems = MasterHolder.BlockMaster.GetBlockMaster(ForUnitTestModBlockId.BlockId).RequiredItems;
            var inventory = new List<global::Core.Item.Interface.IItemStack>
            {
                ServerContext.ItemStackFactory.Create(MasterHolder.ItemMaster.GetItemId(Material1Guid), 4),
                ServerContext.ItemStackFactory.Create(MasterHolder.ItemMaster.GetItemId(Material2Guid), 2),
            };

            Assert.IsEmpty(ConstructionCostShortageCalculator.Calculate(requiredItems, 2, inventory));
        }

        [Test]
        public void エンティティ列は素材を合算し未所持は所持0で返す()
        {
            CreateServer();
            var requiredItems = MasterHolder.BlockMaster.GetBlockMaster(ForUnitTestModBlockId.BlockId).RequiredItems;
            var entityCosts = new List<Mooresmaster.Model.BlocksModule.ConstructionRequiredItemElement[]> { requiredItems, requiredItems, requiredItems };
            var inventory = new List<global::Core.Item.Interface.IItemStack>();

            var shortages = ConstructionCostShortageCalculator.Calculate(entityCosts, inventory);

            Assert.AreEqual(2, shortages.Count);
            Assert.AreEqual(MasterHolder.ItemMaster.GetItemId(Material1Guid), shortages[0].ItemId);
            Assert.AreEqual(0, shortages[0].Held);
            Assert.AreEqual(6, shortages[0].Required);
            Assert.AreEqual(MasterHolder.ItemMaster.GetItemId(Material2Guid), shortages[1].ItemId);
            Assert.AreEqual(3, shortages[1].Required);
        }

        [Test]
        public void セル数0やコスト無しは空を返す()
        {
            CreateServer();
            var requiredItems = MasterHolder.BlockMaster.GetBlockMaster(ForUnitTestModBlockId.BlockId).RequiredItems;
            var inventory = new List<global::Core.Item.Interface.IItemStack>();

            Assert.IsEmpty(ConstructionCostShortageCalculator.Calculate(requiredItems, 0, inventory));
            Assert.IsEmpty(ConstructionCostShortageCalculator.Calculate(Array.Empty<Mooresmaster.Model.BlocksModule.ConstructionRequiredItemElement>(), 3, inventory));
        }

        private static void CreateServer()
        {
            new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
        }
    }
}
