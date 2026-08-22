using System;
using System.Collections.Generic;
using Client.Game.InGame.Construction;
using Core.Master;
using Game.Context;
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;

namespace Client.Tests.Construction
{
    public class ConstructionAffordabilityTest
    {
        private static readonly Guid Material1Guid = Guid.Parse("00000000-0000-0000-1234-000000000003"); // Test3(コスト×2)
        private static readonly Guid Material2Guid = Guid.Parse("00000000-0000-0000-1234-000000000004"); // Test4(コスト×1)

        [Test]
        public void 素材所持数から設置可能セル数を算出する()
        {
            CreateServer();
            var requiredItems = MasterHolder.BlockMaster.GetBlockMaster(ForUnitTestModBlockId.BlockId).RequiredItems;

            // Test3=5個(2セル半) Test4=2個(2セル) → 賄えるのは2セル
            // Test3=5 (2.5 cells) and Test4=2 (2 cells) afford exactly 2 cells
            var inventory = CreateInventory(5, 2);

            Assert.AreEqual(2, ConstructionMaterialAffordability.CalculateAffordableCellCount(requiredItems, inventory));
        }

        [Test]
        public void コスト未定義ならMaxValueを返す()
        {
            CreateServer();
            var requiredItems = MasterHolder.BlockMaster.GetBlockMaster(ForUnitTestModBlockId.BeltConveyorId).RequiredItems;

            Assert.AreEqual(int.MaxValue, ConstructionMaterialAffordability.CalculateAffordableCellCount(requiredItems, new List<global::Core.Item.Interface.IItemStack>()));
        }

        [Test]
        public void 素材が1種でも足りなければ0セル()
        {
            CreateServer();
            var requiredItems = MasterHolder.BlockMaster.GetBlockMaster(ForUnitTestModBlockId.BlockId).RequiredItems;

            // Test4を持っていないため0セル
            // Zero cells because no Test4 is held
            var inventory = CreateInventory(10, 0);

            Assert.AreEqual(0, ConstructionMaterialAffordability.CalculateAffordableCellCount(requiredItems, inventory));
        }

        [Test]
        public void 残り設置数と買えるセット数から置ける数を算出する()
        {
            CreateServer();
            var blockId = ForUnitTestModBlockId.GearBeltConveyor;
            var datastore = new ClientRemainingPlacementCountDatastore();
            datastore.ApplyAll(new Dictionary<BlockId, int> { { blockId, 1 } });

            // 素材2セット+残1 → 1+2×3=7
            // Materials cover two sets and one placement remains in the wallet → 1 + 2x3 = 7
            Assert.AreEqual(7, datastore.GetAffordablePlacementCount(blockId, CreateInventory(2, 2)));
        }

        [Test]
        public void 設置数1なら素材セル数がそのまま置ける数になる()
        {
            CreateServer();
            var datastore = new ClientRemainingPlacementCountDatastore();

            Assert.AreEqual(2, datastore.GetAffordablePlacementCount(ForUnitTestModBlockId.BlockId, CreateInventory(5, 2)));
        }

        [Test]
        public void コスト未定義なら残り設置数に関わらずMaxValue()
        {
            CreateServer();
            var datastore = new ClientRemainingPlacementCountDatastore();

            Assert.AreEqual(int.MaxValue, datastore.GetAffordablePlacementCount(ForUnitTestModBlockId.BeltConveyorId, new List<global::Core.Item.Interface.IItemStack>()));
        }

        [Test]
        public void 坂ベルトの残り設置数は直線代表の財布から引く()
        {
            CreateServer();
            var datastore = new ClientRemainingPlacementCountDatastore();
            datastore.ApplyAll(new Dictionary<BlockId, int> { { ForUnitTestModBlockId.GearBeltConveyor, 2 } });

            // 呼び出し側は財布キーを知らずに坂ベルトのIDをそのまま渡せる
            // A caller can hand over the slope belt id directly without knowing anything about wallet keys
            Assert.AreEqual(2, datastore.GetRemainingCount(ForUnitTestModBlockId.TestGearBeltConveyorUp));
        }

        private static List<global::Core.Item.Interface.IItemStack> CreateInventory(int material1Count, int material2Count)
        {
            var factory = ServerContext.ItemStackFactory;
            var inventory = new List<global::Core.Item.Interface.IItemStack>();
            if (0 < material1Count) inventory.Add(factory.Create(MasterHolder.ItemMaster.GetItemId(Material1Guid), material1Count));
            if (0 < material2Count) inventory.Add(factory.Create(MasterHolder.ItemMaster.GetItemId(Material2Guid), material2Count));
            return inventory;
        }

        private static void CreateServer()
        {
            new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
        }
    }
}
