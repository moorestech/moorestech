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
using Game.Blueprint;
using Client.Tests.PlaceSystem.Blueprint;
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
    /// <summary>
    /// ビルドメニューDTO・選択・BP削除契約の回帰試験
    /// Regression tests for build-menu DTO, selection, and blueprint deletion
    /// </summary>
    public class BuildMenuEntryDtoFactoryTest
    {
        private static readonly HashSet<string> AllowedKinds = new() { "block", "trainCar", "connectTool", "blueprintCopy", "blueprint" };

        [Test]
        public void CreateDtosは全件がGuid形状のidと契約5値のkindをユニークに持つ()
        {
            var (_, _) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var unlockState = new AllPlacementTargetsUnlockedStateData();
            var blueprintGuid = Guid.Parse("70000000-0000-4000-8000-000000000001");

            // BP本体を解決済みの対象をDTO化
            // Convert a target whose blueprint body has resolved into a DTO.
            var library = new BlueprintLookupStub(new BlueprintJsonObject("starter-base", new(), new(), new(), blueprintGuid));
            var targets = new PlacementTargetResolver(new PlacementTargetCatalog(new BeltConveyorPlacementUnlockSourceMap()), library, unlockState)
                .CreateUnlockedTargets();
            var dtos = BuildMenuEntryDtoFactory.CreateDtos(targets, new ConstructionWalletQuery(new ClientRemainingPlacementCountDatastore()), Array.Empty<IItemStack>(), new BlueprintThumbnailContainer());

            // 実マスタ規模で複数エントリが返ること（空リストでは以降の検証が無意味）
            // Multiple entries must come back at real-master scale (an empty list would make the rest of this test meaningless)
            Assert.Greater(dtos.Count, 0);

            foreach (var dto in dtos)
            {
                Assert.IsTrue(Guid.TryParse(dto.Id, out _), $"id is not GUID-shaped: {dto.Id}");
                Assert.IsTrue(AllowedKinds.Contains(dto.Kind), $"kind is not one of the contract's 5 values: {dto.Kind}");
                Assert.IsTrue(Guid.TryParse(dto.CategoryGuid, out _), $"categoryGuid is not GUID-shaped: {dto.CategoryGuid}");
                Assert.IsTrue(Guid.TryParse(dto.SubCategoryGuid, out _), $"subCategoryGuid is not GUID-shaped: {dto.SubCategoryGuid}");
                if (dto.Kind == "blueprint")
                    Assert.IsNotEmpty(dto.Label);
                else
                    Assert.IsNull(dto.Label, $"master-derived {dto.Kind} must not carry a raw label");
            }

            var ids = dtos.Select(d => d.Id).ToList();
            CollectionAssert.AllItemsAreUnique(ids);
            CollectionAssert.AreEquivalent(AllowedKinds, dtos.Select(dto => dto.Kind).Distinct());

            var blueprint = dtos.Single(dto => dto.Id == blueprintGuid.ToString("D"));
            Assert.AreEqual("blueprint", blueprint.Kind);
            Assert.AreEqual("starter-base", blueprint.Label);

            var trainCar = dtos.First(dto => dto.Kind == "trainCar");
            Assert.IsNull(trainCar.Label);
            Assert.IsNotEmpty(trainCar.IconUrl);
            Assert.IsTrue(Guid.TryParse(trainCar.CategoryGuid, out _));
            Assert.IsTrue(Guid.TryParse(trainCar.SubCategoryGuid, out _));
        }

        [Test]
        public void CreateDtosは財布キー正規化後の残り設置数を直線と坂の両方へ反映する()
        {
            var (_, _) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));

            // 直線と坂族は同一。キー正規化は直線
            // Straight and slope share a family; key normalizes to straight
            var straightGuid = Guid.Parse("00000000-0000-0000-0000-000000000015");
            var upGuid = Guid.Parse("00000000-0000-0000-0000-0000000000a1");
            var straightBlockId = MasterHolder.BlockMaster.GetBlockId(straightGuid);

            var datastore = new ClientRemainingPlacementCountDatastore();
            datastore.Apply(straightBlockId, 2);
            var walletQuery = new ConstructionWalletQuery(datastore);

            var targets = new IPlacementTarget[]
            {
                new BlockPlacementTarget(straightGuid, null),
                new BlockPlacementTarget(upGuid, null),
                new TrainCarPlacementTarget(MasterHolder.TrainUnitMaster.Train.TrainCars[0].TrainCarGuid),
            };
            var dtos = BuildMenuEntryDtoFactory.CreateDtos(targets, walletQuery, Array.Empty<IItemStack>(), new BlueprintThumbnailContainer());

            var straightDto = dtos.Single(dto => dto.Id == straightGuid.ToString("D"));
            var upDto = dtos.Single(dto => dto.Id == upGuid.ToString("D"));
            var trainCarDto = dtos.Single(dto => dto.Kind == "trainCar");

            Assert.AreEqual(3, straightDto.SetPlacement.PerCost);
            Assert.AreEqual(2, straightDto.SetPlacement.Remaining);
            Assert.AreEqual(3, upDto.SetPlacement.PerCost);
            Assert.AreEqual(2, upDto.SetPlacement.Remaining);
            // 非ブロックは財布を持たない（配信時にキーごと省略される）
            // Non-block kinds have no wallet at all, so the key is omitted on the wire
            Assert.IsNull(trainCarDto.SetPlacement);
        }

        [Test]
        public void 財布を使わないブロックはSetPlacementを持たない()
        {
            var (_, _) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));

            var walletQuery = new ConstructionWalletQuery(new ClientRemainingPlacementCountDatastore());
            var blockGuid = MasterHolder.BlockMaster.GetBlockMaster(ForUnitTestModBlockId.BlockId).BlockGuid;
            var targets = new IPlacementTarget[] { new BlockPlacementTarget(blockGuid, null) };

            var dto = BuildMenuEntryDtoFactory.CreateDtos(targets, walletQuery, Array.Empty<IItemStack>(), new BlueprintThumbnailContainer())[0];

            Assert.IsNull(dto.SetPlacement);
        }

        [Test]
        public void CreateCategoryDtosはマスタ定義順のGuidを維持する()
        {
            var (_, _) = new MoorestechServerDIContainerGenerator().Create(
                new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var actual = BuildMenuEntryDtoFactory.CreateCategoryDtos();
            var expected = MasterHolder.BuildMenuCategoryMaster.Categories;

            Assert.AreEqual(expected.Length, actual.Count);
            for (var index = 0; index < expected.Length; index++)
            {
                Assert.AreEqual(expected[index].CategoryGuid.ToString("D"), actual[index].CategoryGuid);
                CollectionAssert.AreEqual(
                    expected[index].SubCategories.Select(value => value.SubCategoryGuid.ToString("D")),
                    actual[index].SubCategoryGuids);
            }
        }

        [Test]
        public void 撮影済みBPだけアイコンURLを出す()
        {
            var guid = Guid.NewGuid();
            var target = new BlueprintPlacementTarget(guid, "test", new global::Game.Blueprint.BlueprintJsonObject());
            var thumbnails = new BlueprintThumbnailContainer();

            Assert.IsNull(BuildMenuEntryDtoFactory.ResolveIconUrl(target, thumbnails));
            thumbnails.Add(guid, UnityEngine.Texture2D.whiteTexture);
            Assert.AreEqual($"/api/blueprint-icons/{guid:D}.png", BuildMenuEntryDtoFactory.ResolveIconUrl(target, thumbnails));
        }

        /// <summary>
        /// ブロック・車両・接続ツールを全解放するスタブ
        /// Stub that unlocks every block, train car, and connect tool
        /// </summary>
        private class AllPlacementTargetsUnlockedStateData : IGameUnlockStateData
        {
            public IReadOnlyDictionary<Guid, BlockUnlockStateInfo> BlockUnlockStateInfos { get; } =
                MasterHolder.BlockMaster.Blocks.Data.ToDictionary(b => b.BlockGuid, b => new BlockUnlockStateInfo(b.BlockGuid, true));

            public IReadOnlyDictionary<Guid, ConnectToolUnlockStateInfo> ConnectToolUnlockStateInfos { get; } =
                MasterHolder.ConnectToolMaster.All.ToDictionary(c => c.ConnectToolGuid, c => new ConnectToolUnlockStateInfo(c.ConnectToolGuid, true));

            public IReadOnlyDictionary<Guid, CraftRecipeUnlockStateInfo> CraftRecipeUnlockStateInfos { get; } = new Dictionary<Guid, CraftRecipeUnlockStateInfo>();
            public IReadOnlyDictionary<ItemId, ItemUnlockStateInfo> ItemUnlockStateInfos { get; } = new Dictionary<ItemId, ItemUnlockStateInfo>();
            public IReadOnlyDictionary<Guid, ChallengeCategoryUnlockStateInfo> ChallengeCategoryUnlockStateInfos { get; } = new Dictionary<Guid, ChallengeCategoryUnlockStateInfo>();
            public IReadOnlyDictionary<Guid, MachineRecipeUnlockStateInfo> MachineRecipeUnlockStateInfos { get; } = new Dictionary<Guid, MachineRecipeUnlockStateInfo>();
            public IReadOnlyDictionary<Guid, TrainCarUnlockStateInfo> TrainCarUnlockStateInfos { get; } =
                MasterHolder.TrainUnitMaster.Train.TrainCars.ToDictionary(
                    trainCar => trainCar.TrainCarGuid,
                    trainCar => new TrainCarUnlockStateInfo(trainCar.TrainCarGuid, true));

            public bool IsBlueprintUnlocked => true;
        }
    }
}
