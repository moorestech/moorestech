using System;
using System.Collections.Generic;
using Client.Game.InGame.BlockSystem.PlaceSystem.Util;
using Core.Master;
using Game.Context;
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
        public void 素材所持数から設置可能セル数を算出する()
        {
            CreateServer();
            var requiredItems = MasterHolder.BlockMaster.GetBlockMaster(ForUnitTestModBlockId.BlockId).RequiredItems;
            var factory = ServerContext.ItemStackFactory;

            // Test3=5個(2セル半) Test4=2個(2セル) → 賄えるのは2セル
            // Test3=5 (2.5 cells) and Test4=2 (2 cells) afford exactly 2 cells
            var inventory = new List<global::Core.Item.Interface.IItemStack>
            {
                factory.Create(MasterHolder.ItemMaster.GetItemId(Material1Guid), 5),
                factory.Create(MasterHolder.ItemMaster.GetItemId(Material2Guid), 2),
            };

            Assert.AreEqual(2, ConstructionCostPreviewCalculator.CalculateAffordableCellCount(requiredItems, inventory));
        }

        [Test]
        public void コスト未定義ならMaxValueを返す()
        {
            CreateServer();
            var requiredItems = MasterHolder.BlockMaster.GetBlockMaster(ForUnitTestModBlockId.BeltConveyorId).RequiredItems;

            Assert.AreEqual(int.MaxValue, ConstructionCostPreviewCalculator.CalculateAffordableCellCount(requiredItems, new List<global::Core.Item.Interface.IItemStack>()));
        }

        [Test]
        public void 素材が1種でも足りなければ0セル()
        {
            CreateServer();
            var requiredItems = MasterHolder.BlockMaster.GetBlockMaster(ForUnitTestModBlockId.BlockId).RequiredItems;
            var factory = ServerContext.ItemStackFactory;

            // Test4を持っていないため0セル
            // Zero cells because no Test4 is held
            var inventory = new List<global::Core.Item.Interface.IItemStack>
            {
                factory.Create(MasterHolder.ItemMaster.GetItemId(Material1Guid), 10),
            };

            Assert.AreEqual(0, ConstructionCostPreviewCalculator.CalculateAffordableCellCount(requiredItems, inventory));
        }

        [Test]
        public void 残り設置数と買えるセット数から置ける数を算出する()
        {
            CreateServer();
            var requiredItems = MasterHolder.BlockMaster.GetBlockMaster(ForUnitTestModBlockId.GearBeltConveyor).RequiredItems;
            var factory = ServerContext.ItemStackFactory;
            // GearBeltConveyorのRequiredItemsは1セット=Material1×1+Material2×1（N=3）。素材は2セット分、残り設置数1 → 1 + 2×3 = 7
            // GearBeltConveyor's RequiredItems is 1 set = Material1×1 + Material2×1 (N=3); materials cover two sets, one placement remains → 1 + 2×3 = 7
            var inventory = new List<global::Core.Item.Interface.IItemStack>
            {
                factory.Create(MasterHolder.ItemMaster.GetItemId(Material1Guid), 2),
                factory.Create(MasterHolder.ItemMaster.GetItemId(Material2Guid), 2),
            };

            Assert.AreEqual(7, ConstructionCostPreviewCalculator.CalculateAffordablePlacementCount(requiredItems, 3, 1, inventory));
        }

        [Test]
        public void 設置数1なら従来のセル数計算と一致する()
        {
            CreateServer();
            var requiredItems = MasterHolder.BlockMaster.GetBlockMaster(ForUnitTestModBlockId.BlockId).RequiredItems;
            var factory = ServerContext.ItemStackFactory;
            var inventory = new List<global::Core.Item.Interface.IItemStack>
            {
                factory.Create(MasterHolder.ItemMaster.GetItemId(Material1Guid), 5),
                factory.Create(MasterHolder.ItemMaster.GetItemId(Material2Guid), 2),
            };

            Assert.AreEqual(2, ConstructionCostPreviewCalculator.CalculateAffordablePlacementCount(requiredItems, 1, 0, inventory));
        }

        [Test]
        public void コスト未定義なら残り設置数に関わらずMaxValue()
        {
            CreateServer();
            var requiredItems = MasterHolder.BlockMaster.GetBlockMaster(ForUnitTestModBlockId.BeltConveyorId).RequiredItems;
            Assert.AreEqual(int.MaxValue, ConstructionCostPreviewCalculator.CalculateAffordablePlacementCount(requiredItems, 1, 0, new List<global::Core.Item.Interface.IItemStack>()));
        }

        private static void CreateServer()
        {
            new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
        }
    }
}
