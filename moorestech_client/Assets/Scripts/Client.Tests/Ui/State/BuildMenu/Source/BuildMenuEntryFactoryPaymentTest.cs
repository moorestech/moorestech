using System;
using System.Collections.Generic;
using System.Linq;
using Client.Game.InGame.BlockSystem.PlaceSystem.Blueprint.Thumbnail;
using Client.Game.InGame.BlockSystem.PlaceSystem.Targets;
using Client.Game.InGame.Construction;
using Client.Ui.State.BuildMenu.Entries;
using Client.Ui.State.BuildMenu.Source;
using Common.Debug;
using Core.Item.Interface;
using Core.Master;
using Game.Construction;
using Game.Context;
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;

namespace Client.Tests.Ui.State.BuildMenu.Source
{
    /// <summary>
    /// 建築メニューのエントリの支払いの免除・素材の所持と不足・1 セット設置を確かめる（L0）
    /// Verifies build-menu entry payment waivers, material held/short counts and set placement (L0)
    /// </summary>
    public class BuildMenuEntryFactoryPaymentTest
    {
        private static readonly Guid StraightBeltGuid = Guid.Parse("00000000-0000-0000-0000-000000000015");
        private static readonly Guid UpSlopeBeltGuid = Guid.Parse("00000000-0000-0000-0000-0000000000a1");

        [SetUp]
        public void SetUp()
        {
            new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
        }

        [TearDown]
        public void TearDown()
        {
            DebugParameters.RemoveBool(DebugParameterKeys.FreeBlockPlacement);
        }

        [Test]
        public void 財布の残りが賄うブロックは免除し素材は事実のまま不足を出す()
        {
            var datastore = new ClientRemainingPlacementCountDatastore();
            datastore.Apply(MasterHolder.BlockMaster.GetBlockId(StraightBeltGuid), 2);

            var entry = CreateOne(new BlockPlacementTarget(StraightBeltGuid, null), datastore, Array.Empty<IItemStack>());

            Assert.IsTrue(entry.Catalog.PaymentWaived);
            Assert.Greater(entry.Materials.Count, 0);
            foreach (var line in entry.Materials)
            {
                Assert.AreEqual(0, line.Row.Item.Held);
                Assert.Less(line.Row.Item.Held, line.Row.Item.Count);
            }
        }

        [Test]
        public void 財布が空なら所持数と必要数を比べて不足を出す()
        {
            var required = MasterHolder.BlockMaster.GetBlockMaster(StraightBeltGuid).RequiredItems[0];
            var firstItemId = MasterHolder.ItemMaster.GetItemId(required.ItemGuid);
            var held = new List<IItemStack> { ServerContext.ItemStackFactory.Create(firstItemId, required.Count - 1) };

            var entry = CreateOne(new BlockPlacementTarget(StraightBeltGuid, null), new ClientRemainingPlacementCountDatastore(), held);

            Assert.IsFalse(entry.Catalog.PaymentWaived);
            var first = entry.Materials.Single(line => line.Row.ItemId == firstItemId);
            Assert.AreEqual(required.Count - 1, first.Row.Item.Held);
            Assert.AreEqual(required.Count, first.Row.Item.Count);
            Assert.IsTrue(first.IsInsufficient);
        }

        [Test]
        public void 財布を使わないブロックは所持が必要数に届けば不足でない()
        {
            var blockGuid = MasterHolder.BlockMaster.GetBlockMaster(ForUnitTestModBlockId.BlockId).BlockGuid;
            var required = MasterHolder.BlockMaster.GetBlockMaster(blockGuid).RequiredItems[0];
            var firstItemId = MasterHolder.ItemMaster.GetItemId(required.ItemGuid);
            var held = new List<IItemStack> { ServerContext.ItemStackFactory.Create(firstItemId, required.Count) };

            var entry = CreateOne(new BlockPlacementTarget(blockGuid, null), new ClientRemainingPlacementCountDatastore(), held);

            Assert.IsFalse(entry.Catalog.PaymentWaived);
            var first = entry.Materials.Single(line => line.Row.ItemId == firstItemId);
            Assert.AreEqual(required.Count, first.Row.Item.Held);
            Assert.IsFalse(first.IsInsufficient);
        }

        [Test]
        public void 無料設置中はブロックだけを免除し素材行は事実のまま()
        {
            var blockGuid = MasterHolder.BlockMaster.GetBlockMaster(ForUnitTestModBlockId.BlockId).BlockGuid;
            DebugParameters.SaveBool(DebugParameterKeys.FreeBlockPlacement, true);

            var entry = CreateOne(new BlockPlacementTarget(blockGuid, null), new ClientRemainingPlacementCountDatastore(), Array.Empty<IItemStack>());

            Assert.IsTrue(entry.Catalog.PaymentWaived);
            Assert.Greater(entry.Materials.Count, 0);
            foreach (var line in entry.Materials) Assert.AreEqual(0, line.Row.Item.Held);
        }

        [Test]
        public void 無料設置中でも車両と接続ツールは免除しない()
        {
            // 車両と接続ツールの設置はこのフラグを見ないので、表示だけ免除すると実挙動と食い違う
            // Train-car and connect-tool placement ignore this flag, so waiving only in the display would contradict the real behavior
            DebugParameters.SaveBool(DebugParameterKeys.FreeBlockPlacement, true);
            var targets = new IPlacementTarget[]
            {
                new TrainCarPlacementTarget(MasterHolder.TrainUnitMaster.Train.TrainCars[0].TrainCarGuid),
                new ConnectToolPlacementTarget(MasterHolder.ConnectToolMaster.All[0].ConnectToolGuid),
            };

            var entries = BuildMenuEntryFactory.CreateEntries(targets, new ConstructionWalletQuery(new ClientRemainingPlacementCountDatastore()), Array.Empty<IItemStack>(), new BlueprintThumbnailContainer());

            Assert.AreEqual(2, entries.Count);
            foreach (var entry in entries) Assert.IsFalse(entry.Catalog.PaymentWaived, entry.KindName);
        }

        [Test]
        public void 財布のある直線と坂は同じ1セット設置を持ち車両と財布なしブロックは持たない()
        {
            // 直線と坂は同じ財布族で、キーは直線へ正規化される
            // Straight and slope share one wallet family whose key normalizes to the straight belt
            var datastore = new ClientRemainingPlacementCountDatastore();
            datastore.Apply(MasterHolder.BlockMaster.GetBlockId(StraightBeltGuid), 2);
            var plainBlockGuid = MasterHolder.BlockMaster.GetBlockMaster(ForUnitTestModBlockId.BlockId).BlockGuid;
            var targets = new IPlacementTarget[]
            {
                new BlockPlacementTarget(StraightBeltGuid, null),
                new BlockPlacementTarget(UpSlopeBeltGuid, null),
                new TrainCarPlacementTarget(MasterHolder.TrainUnitMaster.Train.TrainCars[0].TrainCarGuid),
                new BlockPlacementTarget(plainBlockGuid, null),
            };

            var entries = BuildMenuEntryFactory.CreateEntries(targets, new ConstructionWalletQuery(datastore), Array.Empty<IItemStack>(), new BlueprintThumbnailContainer());

            foreach (var slopeOrStraight in entries.Take(2))
            {
                var available = (BuildMenuSetPlacementAvailable)slopeOrStraight.SetPlacement;
                Assert.AreEqual(3, available.PerCost);
                Assert.AreEqual(2, available.Remaining);
            }
            Assert.AreSame(BuildMenuSetPlacementNone.Instance, entries[2].SetPlacement);
            Assert.AreSame(BuildMenuSetPlacementNone.Instance, entries[3].SetPlacement);
        }

        private static BuildMenuEntryViewModel CreateOne(IPlacementTarget target, ClientRemainingPlacementCountDatastore datastore, IEnumerable<IItemStack> held)
        {
            return BuildMenuEntryFactory.CreateEntries(new[] { target }, new ConstructionWalletQuery(datastore), held, new BlueprintThumbnailContainer())[0];
        }
    }
}
