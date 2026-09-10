using System;
using System.Collections.Generic;
using Client.Game.InGame.BlockSystem.PlaceSystem.Util;
using Client.Game.InGame.Construction;
using Core.Master;
using Game.Construction;
using Game.Context;
using NUnit.Framework;
using Server.Boot;
using Server.Protocol.PacketResponse;
using Tests.Module.TestMod;

// namespaceは既存の隣接テスト（CommonBlockPlacePointCalculatorTest等）に合わせること
// Match the namespace of sibling tests such as CommonBlockPlacePointCalculatorTest
namespace Client.Tests.PlaceSystem
{
    public class ConstructionCostPreviewMarkerTest
    {
        private static readonly Guid Material1Guid = Guid.Parse("00000000-0000-0000-1234-000000000003"); // Test3(コスト×2)
        private static readonly Guid Material2Guid = Guid.Parse("00000000-0000-0000-1234-000000000004"); // Test4(コスト×1)

        [Test]
        public void 賄えないセルをPlaceableFalseへ書き換える()
        {
            CreateServer();
            var blockId = ForUnitTestModBlockId.GearBeltConveyor; // PlacementsPerCost=3, RequiredItems=Material1×1+Material2×1
            var factory = ServerContext.ItemStackFactory;
            var inventory = new List<global::Core.Item.Interface.IItemStack>
            {
                factory.Create(MasterHolder.ItemMaster.GetItemId(Material1Guid), 1),
                factory.Create(MasterHolder.ItemMaster.GetItemId(Material2Guid), 1),
            };
            var walletQuery = new ConstructionWalletQuery(new ClientRemainingPlacementCountDatastore());

            // 財布0・素材1セット→1セット×N=3
            // Empty wallet, one set of materials → one set × N = 3 placements
            var placeInfos = new List<PlaceInfo>();
            for (var i = 0; i < 5; i++) placeInfos.Add(new PlaceInfo { BlockId = blockId, Placeable = true });

            ConstructionCostPreviewMarker.MarkUnaffordableCellsAsNotPlaceable(placeInfos, walletQuery, inventory);

            Assert.IsTrue(placeInfos[0].Placeable);
            Assert.IsTrue(placeInfos[1].Placeable);
            Assert.IsTrue(placeInfos[2].Placeable);
            Assert.IsFalse(placeInfos[3].Placeable);
            Assert.IsFalse(placeInfos[4].Placeable);
        }

        [Test]
        public void BlockIdが混ざる列はブロックごとに賄える数を数える()
        {
            CreateServer();

            // 素材1セット分だけ持つ。Test3×2が要るBlockIdは0セル、Test3×1のGearBeltConveyorは3セル置ける
            // Holding one set of materials: BlockId needs Test3x2 so 0 cells, GearBeltConveyor needs Test3x1 so 3 cells
            var factory = ServerContext.ItemStackFactory;
            var inventory = new List<global::Core.Item.Interface.IItemStack>
            {
                factory.Create(MasterHolder.ItemMaster.GetItemId(Material1Guid), 1),
                factory.Create(MasterHolder.ItemMaster.GetItemId(Material2Guid), 1),
            };
            var walletQuery = new ConstructionWalletQuery(new ClientRemainingPlacementCountDatastore());

            // 賄えない方を先頭に置く（代表セル方式なら以降の全セルを巻き添えで不可にする並び）
            // The unaffordable block leads the run, the ordering a representative cell would drag every later cell down with
            var placeInfos = new List<PlaceInfo> { new() { BlockId = ForUnitTestModBlockId.BlockId, Placeable = true } };
            for (var i = 0; i < 4; i++) placeInfos.Add(new PlaceInfo { BlockId = ForUnitTestModBlockId.GearBeltConveyor, Placeable = true });

            ConstructionCostPreviewMarker.MarkUnaffordableCellsAsNotPlaceable(placeInfos, walletQuery, inventory);

            Assert.IsFalse(placeInfos[0].Placeable);
            Assert.IsTrue(placeInfos[1].Placeable);
            Assert.IsTrue(placeInfos[2].Placeable);
            Assert.IsTrue(placeInfos[3].Placeable);
            Assert.IsFalse(placeInfos[4].Placeable);
        }

        private static void CreateServer()
        {
            new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
        }
    }
}
