using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Client.Game.InGame.BlockSystem.PlaceSystem.Blueprint;
using Client.Game.InGame.BlockSystem.PlaceSystem.Blueprint.Thumbnail;
using Client.Game.InGame.BlockSystem.PlaceSystem.Targets;
using Client.Game.InGame.Construction;
using Game.Construction;
using Game.PlacementTarget;
using Client.Game.InGame.UI.BuildMenu;
using Client.Game.InGame.UI.UIState;
using Client.WebUiHost.Game.Actions;
using Client.WebUiHost.Game.Topics.BuildMenu;
using Common.Debug;
using Core.Item.Interface;
using Core.Master;
using Cysharp.Threading.Tasks;
using Game.Block.Interface.Extension;
using Game.Context;
using Game.UnlockState;
using Game.UnlockState.States;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;
using UnityEngine;

namespace Client.Tests.WebUi
{
    public class BuildMenuWalletDtoTest
    {
        [Test]
        public void 財布に残りがあるブロックは支払い免除として配る()
        {
            var (_, _) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));

            var straightGuid = Guid.Parse("00000000-0000-0000-0000-000000000015");
            var straightBlockId = MasterHolder.BlockMaster.GetBlockId(straightGuid);
            var datastore = new ClientRemainingPlacementCountDatastore();
            datastore.Apply(straightBlockId, 2);
            var walletQuery = new ConstructionWalletQuery(datastore);

            var targets = new IPlacementTarget[] { new BlockPlacementTarget(straightGuid, null) };
            var dto = BuildMenuEntryDtoFactory.CreateDtos(targets, walletQuery, Array.Empty<IItemStack>(), new BlueprintThumbnailContainer())[0];

            // 素材は事実として不足のまま、免除はエントリ単位で立てる
            // Materials stay factually short while the waiver is raised per entry
            Assert.IsTrue(dto.PaymentWaived);
            Assert.Greater(dto.RequiredItems.Count, 0);
            foreach (var requiredItem in dto.RequiredItems)
            {
                Assert.AreEqual(0, requiredItem.Held);
                Assert.IsTrue(requiredItem.Lacking);
            }
        }

        [Test]
        public void 財布の残りが尽きたブロックは所持数と必要数を突き合わせて不足を出す()
        {
            var (_, _) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));

            var straightGuid = Guid.Parse("00000000-0000-0000-0000-000000000015");
            var walletQuery = new ConstructionWalletQuery(new ClientRemainingPlacementCountDatastore());
            var targets = new IPlacementTarget[] { new BlockPlacementTarget(straightGuid, null) };

            // 1件目だけ-1個で不足1件作る
            // Give the first item one less than required, creating one shortage
            var requiredItems = MasterHolder.BlockMaster.GetBlockMaster(straightGuid).RequiredItems;
            var firstItemId = MasterHolder.ItemMaster.GetItemId(requiredItems[0].ItemGuid);
            var heldCount = requiredItems[0].Count - 1;
            var inventory = new List<IItemStack> { ServerContext.ItemStackFactory.Create(firstItemId, heldCount) };

            var dto = BuildMenuEntryDtoFactory.CreateDtos(targets, walletQuery, inventory, new BlueprintThumbnailContainer())[0];

            Assert.IsFalse(dto.PaymentWaived);
            var first = dto.RequiredItems.Single(item => item.ItemId == firstItemId.AsPrimitive());
            Assert.AreEqual(heldCount, first.Held);
            Assert.IsTrue(first.Lacking);
        }

        [Test]
        public void 無料設置デバッグ中のブロックは支払い免除として配る()
        {
            var (_, _) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));

            var walletQuery = new ConstructionWalletQuery(new ClientRemainingPlacementCountDatastore());
            var blockGuid = MasterHolder.BlockMaster.GetBlockMaster(ForUnitTestModBlockId.BlockId).BlockGuid;
            var targets = new IPlacementTarget[] { new BlockPlacementTarget(blockGuid, null) };

            // Client.Tests は ClientTestsDebugParametersIsolationFixture で隔離済み。後続テストへ残さないよう必ず消す
            // Client.Tests is already isolated by ClientTestsDebugParametersIsolationFixture; still remove it so later tests are unaffected
            DebugParameters.SaveBool(DebugParameterKeys.FreeBlockPlacement, true);
            try
            {
                var dto = BuildMenuEntryDtoFactory.CreateDtos(targets, walletQuery, Array.Empty<IItemStack>(), new BlueprintThumbnailContainer())[0];

                Assert.IsTrue(dto.PaymentWaived);
                Assert.Greater(dto.RequiredItems.Count, 0);
                foreach (var requiredItem in dto.RequiredItems)
                {
                    Assert.AreEqual(0, requiredItem.Held);
                    Assert.IsTrue(requiredItem.Lacking);
                }
            }
            finally
            {
                DebugParameters.RemoveBool(DebugParameterKeys.FreeBlockPlacement);
            }
        }

        [Test]
        public void 無料設置デバッグ中でも車両は支払い免除にしない()
        {
            var (_, _) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));

            var walletQuery = new ConstructionWalletQuery(new ClientRemainingPlacementCountDatastore());
            var trainCarGuid = MasterHolder.TrainUnitMaster.Train.TrainCars[0].TrainCarGuid;
            var targets = new IPlacementTarget[] { new TrainCarPlacementTarget(trainCarGuid) };

            // 車両設置プロトコルはこのフラグを見ないため、表示だけ免除すると実挙動と食い違う
            // The train-car placement protocol ignores this flag, so waiving it only in the display would contradict the real behavior
            DebugParameters.SaveBool(DebugParameterKeys.FreeBlockPlacement, true);
            try
            {
                var dto = BuildMenuEntryDtoFactory.CreateDtos(targets, walletQuery, Array.Empty<IItemStack>(), new BlueprintThumbnailContainer())[0];

                Assert.IsFalse(dto.PaymentWaived);
                Assert.Greater(dto.RequiredItems.Count, 0);
                foreach (var requiredItem in dto.RequiredItems) Assert.IsTrue(requiredItem.Lacking);
            }
            finally
            {
                DebugParameters.RemoveBool(DebugParameterKeys.FreeBlockPlacement);
            }
        }

        [Test]
        public void 財布を使わないブロックは所持数をそのままHeldへ載せる()
        {
            var (_, _) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));

            var walletQuery = new ConstructionWalletQuery(new ClientRemainingPlacementCountDatastore());
            var blockGuid = MasterHolder.BlockMaster.GetBlockMaster(ForUnitTestModBlockId.BlockId).BlockGuid;
            var requiredItems = MasterHolder.BlockMaster.GetBlockMaster(blockGuid).RequiredItems;
            var firstItemId = MasterHolder.ItemMaster.GetItemId(requiredItems[0].ItemGuid);
            var inventory = new List<IItemStack> { ServerContext.ItemStackFactory.Create(firstItemId, requiredItems[0].Count) };

            var targets = new IPlacementTarget[] { new BlockPlacementTarget(blockGuid, null) };
            var dto = BuildMenuEntryDtoFactory.CreateDtos(targets, walletQuery, inventory, new BlueprintThumbnailContainer())[0];

            Assert.IsFalse(dto.PaymentWaived);
            var first = dto.RequiredItems.Single(item => item.ItemId == firstItemId.AsPrimitive());
            Assert.AreEqual(requiredItems[0].Count, first.Held);
            Assert.IsFalse(first.Lacking);
        }

    }
}
