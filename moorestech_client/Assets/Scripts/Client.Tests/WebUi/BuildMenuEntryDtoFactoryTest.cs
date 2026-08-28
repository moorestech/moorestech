using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Client.Game.InGame.BlockSystem.PlaceSystem.Blueprint;
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

            // 解放判定はResolverの責務のため、変換対象の設置対象一覧を直接渡して変換だけを検証する
            // The unlock decision belongs to the resolver, so hand the targets in directly and verify only the conversion
            var targets = new PlacementTargetCatalog()
                .UnlockedEntries(unlockState, false, new[] { (blueprintGuid, "starter-base") })
                .Select(PlacementTargetFactory.Create)
                .ToList();
            var dtos = BuildMenuEntryDtoFactory.CreateDtos(targets, new ConstructionWalletQuery(new ClientRemainingPlacementCountDatastore()), Array.Empty<IItemStack>());

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
            var dtos = BuildMenuEntryDtoFactory.CreateDtos(targets, walletQuery, Array.Empty<IItemStack>());

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

            var dto = BuildMenuEntryDtoFactory.CreateDtos(targets, walletQuery, Array.Empty<IItemStack>())[0];

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
        public void BuildMenuSelectActionは現行カタログだけをBuildMenu中に選択する()
        {
            var (_, _) = new MoorestechServerDIContainerGenerator().Create(
                new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var catalog = new PlacementTargetCatalog();
            var unlocked = new AllPlacementTargetsUnlockedStateData();
            var controlObject = new GameObject("BuildMenuSelectActionTest.Control");
            var viewObject = new GameObject("BuildMenuSelectActionTest.View");
            var control = controlObject.AddComponent<UIStateControl>();
            var view = viewObject.AddComponent<BuildMenuView>();

            try
            {
                SetCurrentState(control, UIStateEnum.BuildMenu);
                var handler = new BuildMenuSelectActionHandler(
                    control, new PlacementTargetResolver(catalog, new ClientBlueprintLibrary(), unlocked), view);
                var entry = catalog.UnlockedEntries(
                    unlocked, false, Array.Empty<(Guid, string)>()).First();

                var success = handler.ExecuteAsync(
                    new JObject { ["id"] = entry.Id.ToString("D") }).GetAwaiter().GetResult();
                Assert.IsTrue(success.Ok);
                Assert.IsTrue(view.TryConsumeSelectedEntry(out var selected));
                Assert.AreEqual(entry.Id, selected.Target.Id);

                var malformed = handler.ExecuteAsync(
                    new JObject { ["id"] = "not-a-guid" }).GetAwaiter().GetResult();
                Assert.AreEqual("invalid_payload", malformed.Error);

                SetCurrentState(control, UIStateEnum.GameScreen);
                var invalidState = handler.ExecuteAsync(
                    new JObject { ["id"] = entry.Id.ToString("D") }).GetAwaiter().GetResult();
                Assert.AreEqual("invalid_state", invalidState.Error);

                SetCurrentState(control, UIStateEnum.BuildMenu);
                var unknown = handler.ExecuteAsync(
                    new JObject { ["id"] = Guid.NewGuid().ToString("D") }).GetAwaiter().GetResult();
                Assert.AreEqual("unknown_entry", unknown.Error);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(viewObject);
                UnityEngine.Object.DestroyImmediate(controlObject);
            }

            #region Internal

            void SetCurrentState(UIStateControl control, UIStateEnum state)
            {
                typeof(UIStateControl).GetProperty(nameof(UIStateControl.CurrentState)).SetValue(control, state);
            }

            #endregion
        }

        [TestCase(BlueprintDeleteResult.Success, true, null)]
        [TestCase(BlueprintDeleteResult.NotFound, false, "blueprint_delete_not_found")]
        [TestCase(BlueprintDeleteResult.NotUnlocked, false, "blueprint_delete_not_unlocked")]
        [TestCase(BlueprintDeleteResult.RequestFailed, false, "blueprint_delete_request_failed")]
        public void BlueprintDeleteActionは削除結果をエラー契約へ変換する(
            BlueprintDeleteResult deleteResult, bool expectedOk, string expectedError)
        {
            var service = new BlueprintDeleteServiceStub(deleteResult);
            var handler = new BlueprintDeleteActionHandler(service);
            var blueprintGuid = Guid.NewGuid();
            var result = handler.ExecuteAsync(
                new JObject { ["id"] = blueprintGuid.ToString("D") }).GetAwaiter().GetResult();
            Assert.AreEqual(expectedOk, result.Ok);
            Assert.AreEqual(expectedError, result.Error);
            Assert.AreEqual(blueprintGuid, service.LastBlueprintGuid);
        }

        [Test]
        public void 財布に残りがあるブロックは所持ゼロでも不足にならない()
        {
            var (_, _) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));

            var straightGuid = Guid.Parse("00000000-0000-0000-0000-000000000015");
            var straightBlockId = MasterHolder.BlockMaster.GetBlockId(straightGuid);
            var datastore = new ClientRemainingPlacementCountDatastore();
            datastore.Apply(straightBlockId, 2);
            var walletQuery = new ConstructionWalletQuery(datastore);

            var targets = new IPlacementTarget[] { new BlockPlacementTarget(straightGuid, null) };
            var dto = BuildMenuEntryDtoFactory.CreateDtos(targets, walletQuery, Array.Empty<IItemStack>())[0];

            Assert.Greater(dto.RequiredItems.Count, 0);
            foreach (var requiredItem in dto.RequiredItems)
            {
                Assert.AreEqual(0, requiredItem.Held);
                Assert.IsFalse(requiredItem.Lacking, "残りがある間は不足にしない");
            }
        }

        [Test]
        public void 財布の残りが尽きたブロックは所持数と必要数を突き合わせて不足を出す()
        {
            var (_, _) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));

            var straightGuid = Guid.Parse("00000000-0000-0000-0000-000000000015");
            var walletQuery = new ConstructionWalletQuery(new ClientRemainingPlacementCountDatastore());
            var targets = new IPlacementTarget[] { new BlockPlacementTarget(straightGuid, null) };

            // 必要素材の1件目だけを必要数-1だけ持たせ、不足が1件だけ立つ状態を作る
            // Hold one less than required of the first material so exactly one shortage stands
            var requiredItems = MasterHolder.BlockMaster.GetBlockMaster(straightGuid).RequiredItems;
            var firstItemId = MasterHolder.ItemMaster.GetItemId(requiredItems[0].ItemGuid);
            var heldCount = requiredItems[0].Count - 1;
            var inventory = new List<IItemStack> { ServerContext.ItemStackFactory.Create(firstItemId, heldCount) };

            var dto = BuildMenuEntryDtoFactory.CreateDtos(targets, walletQuery, inventory)[0];

            var first = dto.RequiredItems.Single(item => item.ItemId == firstItemId.AsPrimitive());
            Assert.AreEqual(heldCount, first.Held);
            Assert.IsTrue(first.Lacking);
        }

        [Test]
        public void 無料設置デバッグ中は所持ゼロでも不足にしない()
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
                var dto = BuildMenuEntryDtoFactory.CreateDtos(targets, walletQuery, Array.Empty<IItemStack>())[0];

                Assert.Greater(dto.RequiredItems.Count, 0);
                foreach (var requiredItem in dto.RequiredItems)
                {
                    Assert.AreEqual(0, requiredItem.Held);
                    Assert.IsFalse(requiredItem.Lacking, "無料設置中は不足にしない");
                }
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
            var dto = BuildMenuEntryDtoFactory.CreateDtos(targets, walletQuery, inventory)[0];

            var first = dto.RequiredItems.Single(item => item.ItemId == firstItemId.AsPrimitive());
            Assert.AreEqual(requiredItems[0].Count, first.Held);
            Assert.IsFalse(first.Lacking);
        }

        private class BlueprintDeleteServiceStub : IBlueprintDeleteService
        {
            private readonly BlueprintDeleteResult _result;
            public Guid LastBlueprintGuid;

            public BlueprintDeleteServiceStub(BlueprintDeleteResult result) => _result = result;

            public UniTask<BlueprintDeleteResult> DeleteBlueprint(Guid blueprintGuid, CancellationToken ct)
            {
                LastBlueprintGuid = blueprintGuid;
                return UniTask.FromResult(_result);
            }
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
