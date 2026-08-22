using System;
using System.Collections.Generic;
using Client.Game.InGame.BlockSystem.PlaceSystem.Util;
using Client.Game.InGame.Construction;
using Core.Master;
using Game.Context;
using NUnit.Framework;
using Server.Boot;
using Server.Protocol.PacketResponse;
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
            var datastore = new ClientRemainingPlacementCountDatastore();

            // 財布0・素材1セットのみ→1セット×N=3
            // Empty wallet, materials for exactly one set → affords one set × N = 3 placements
            var placeInfos = new List<PlaceInfo>();
            for (var i = 0; i < 5; i++) placeInfos.Add(new PlaceInfo { BlockId = blockId, Placeable = true });

            ConstructionCostPreviewCalculator.MarkUnaffordableCellsAsNotPlaceable(placeInfos, blockId, datastore, inventory);

            Assert.IsTrue(placeInfos[0].Placeable);
            Assert.IsTrue(placeInfos[1].Placeable);
            Assert.IsTrue(placeInfos[2].Placeable);
            Assert.IsFalse(placeInfos[3].Placeable);
            Assert.IsFalse(placeInfos[4].Placeable);
        }

        private static void CreateServer()
        {
            new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
        }
    }
}
