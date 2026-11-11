using System;
using System.Collections.Generic;
using System.Linq;
using Client.Game.InGame.BlockSystem.PlaceSystem.Blueprint.Thumbnail;
using Client.Game.InGame.BlockSystem.PlaceSystem.Targets;
using Client.Game.InGame.Construction;
using Client.Localization;
using Client.Tests.PlaceSystem.Blueprint;
using Client.Ui.State.BuildMenu.Entries;
using Client.Ui.State.BuildMenu.Source;
using Client.Ui.State.PlacementTarget;
using Client.WebUiHost.Game.Topics.BuildMenu;
using Common.Debug;
using Core.Item.Interface;
using Core.Master;
using Game.Block.Interface;
using Game.Block.Interface.Extension;
using Game.Blueprint;
using Game.Construction;
using Game.Context;
using Game.PlacementTarget;
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;
using UnityEngine;

namespace Client.Tests.Ui.State.BuildMenu
{
    /// <summary>
    /// エントリ組み立てを橋と照合
    /// Checks the build-menu entry factory against the Web bridge
    /// </summary>
    public class BuildMenuEntryFactoryTest
    {
        private bool _hadSavedLanguageCode;
        private string _savedLanguageCode;

        [SetUp]
        public void SetUp()
        {
            // 表示名は辞書を引くので、端末の保存言語を退避して英語で辞書を組む（前例 ScreenTestHost）
            // Display names read the dictionary; stash the saved language and compose it in English (precedent ScreenTestHost)
            _hadSavedLanguageCode = PlayerPrefs.HasKey(Localize.LanguagePreferenceKey);
            _savedLanguageCode = PlayerPrefs.GetString(Localize.LanguagePreferenceKey);
            Localize.Initialize();
            Localize.TrySetChosenLanguage("english");
        }

        [TearDown]
        public void TearDown()
        {
            if (_hadSavedLanguageCode) PlayerPrefs.SetString(Localize.LanguagePreferenceKey, _savedLanguageCode);
            else PlayerPrefs.DeleteKey(Localize.LanguagePreferenceKey);
            PlayerPrefs.Save();
            Localize.Initialize();
            DebugParameters.RemoveBool(DebugParameterKeys.FreeBlockPlacement);
        }

        [Test]
        public void 全解放の対象でWebの橋と同じ種別とカテゴリ対と素材と免除と1セット設置を出す()
        {
            AssertMatchesBridge(new ClientRemainingPlacementCountDatastore(), false);
        }

        [Test]
        public void 所持品があるときも橋と同じ所持数と不足を出す()
        {
            var entries = AssertMatchesBridge(new ClientRemainingPlacementCountDatastore(), true);

            // 比べる値が定数でないこと（所持数を持つ素材がある）を確かめる
            // Make sure the compared values are not constants (some material has a held count)
            Assert.IsTrue(entries.Any(entry => entry.Materials.Any(line => 0 < line.Row.Item.Held)));
        }

        [Test]
        public void 財布の残りが賄うブロックは橋と同じく免除になる()
        {
            var datastore = new ClientRemainingPlacementCountDatastore();
            datastore.ApplyAll(new Dictionary<BlockId, int> { { ConstructionWalletQuery.ResolveWalletBlockId(ForUnitTestModBlockId.GearBeltConveyor), 3 } });

            var entries = AssertMatchesBridge(datastore, false);

            Assert.IsTrue(entries.Any(entry => entry.Catalog.PaymentWaived));
        }

        [Test]
        public void 無料設置中は橋と同じくブロックが免除になる()
        {
            DebugParameters.SaveBool(DebugParameterKeys.FreeBlockPlacement, true);

            var entries = AssertMatchesBridge(new ClientRemainingPlacementCountDatastore(), false);

            Assert.IsTrue(entries.Any(entry => entry.Catalog.PaymentWaived));
        }

        private static IReadOnlyList<BuildMenuEntryViewModel> AssertMatchesBridge(ClientRemainingPlacementCountDatastore datastore, bool holdMaterials)
        {
            new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var blueprint = CreateBlueprint();
            var targets = new PlacementTargetResolver(new PlacementTargetCatalog(new BeltConveyorPlacementUnlockSourceMap()), new BlueprintLookupStub(blueprint), new AllPlacementTargetsUnlocked()).CreateUnlockedTargets();
            var wallet = new ConstructionWalletQuery(datastore);
            var thumbnails = new BlueprintThumbnailContainer();
            var held = holdMaterials ? HoldOneOfEveryMaterial() : new List<IItemStack>();

            var entries = BuildMenuEntryFactory.CreateEntries(targets, wallet, held, thumbnails);
            var dtos = BuildMenuEntryDtoFactory.CreateDtos(targets, wallet, held, thumbnails);

            // 同じ順に 1 対 1 で並び、どの欄も橋の DTO と一致する
            // Same order one-to-one, and every field matches the bridge DTO
            Assert.Greater(entries.Count, 0);
            Assert.AreEqual(dtos.Count, entries.Count);
            for (var i = 0; i < dtos.Count; i++)
            {
                var entry = entries[i];
                var dto = dtos[i];
                Assert.AreEqual(dto.Id, entry.Catalog.TargetId.ToString("D"));
                Assert.AreEqual(dto.Kind, entry.KindName);
                Assert.AreEqual(dto.CategoryGuid, entry.Catalog.CategoryGuid.ToString("D"));
                Assert.AreEqual(dto.SubCategoryGuid, entry.Catalog.SubCategoryGuid.ToString("D"));
                Assert.AreEqual(dto.PaymentWaived, entry.Catalog.PaymentWaived);
                Assert.AreEqual(dto.SetPlacement != null, entry.SetPlacement is BuildMenuSetPlacementAvailable, dto.Id);
                CollectionAssert.AreEqual(dto.RequiredItems.Select(item => (item.ItemId, item.Count, item.Held, item.Lacking)), entry.Materials.Select(line => (line.Row.ItemId.AsPrimitive(), line.Row.Item.Count, line.Row.Item.Held, line.Row.Item.Lacking)), dto.Id);
            }
            return entries;

            #region Internal

            // BP（ブロック2つ）で素材合算まで照合
            // Use one non-empty blueprint (two blocks) so the material totals are compared too
            BlueprintJsonObject CreateBlueprint()
            {
                var blocks = new List<BlueprintBlockJsonObject>();
                var blockIds = new[] { ForUnitTestModBlockId.GearBeltConveyor, ForUnitTestModBlockId.BlockId };
                for (var i = 0; i < blockIds.Length; i++)
                    blocks.Add(new BlueprintBlockJsonObject(new Vector3Int(i * 10, 0, 0), MasterHolder.BlockMaster.GetBlockMaster(blockIds[i]).BlockGuid.ToString(), (int)BlockDirection.North, new()));
                return new BlueprintJsonObject("starter-base", blocks, new(), new(), Guid.Parse("70000000-0000-4000-8000-000000000001"));
            }

            // 素材になる全アイテムを 1 個ずつ所持品にする
            // Hold one of every item that appears as a material
            List<IItemStack> HoldOneOfEveryMaterial()
            {
                var probe = BuildMenuEntryFactory.CreateEntries(targets, wallet, Array.Empty<IItemStack>(), thumbnails);
                return probe.SelectMany(entry => entry.Materials).Select(line => line.Row.ItemId).Distinct().Select(itemId => ServerContext.ItemStackFactory.Create(itemId, 1)).ToList();
            }

            #endregion
        }

        [Test]
        public void 撮影済みBPだけBPの絵を指しBPコピーは名前で描く()
        {
            var guid = Guid.NewGuid();
            var target = new BlueprintPlacementTarget(guid, "test", new BlueprintJsonObject());
            var thumbnails = new BlueprintThumbnailContainer();

            Assert.AreSame(PlacementTargetNameOnlyIcon.Instance, PlacementTargetIconResolver.Resolve(target, thumbnails));
            thumbnails.Add(guid, Texture2D.whiteTexture);
            Assert.AreEqual(guid, ((PlacementTargetBlueprintIcon)PlacementTargetIconResolver.Resolve(target, thumbnails)).BlueprintGuid);
            Assert.AreSame(PlacementTargetNameOnlyIcon.Instance, PlacementTargetIconResolver.Resolve(new BlueprintCopyPlacementTarget(Guid.NewGuid()), thumbnails));
        }

        [Test]
        public void ブロックはブロックの絵を引く()
        {
            new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var blockGuid = MasterHolder.BlockMaster.GetBlockMaster(ForUnitTestModBlockId.BlockId).BlockGuid;

            var icon = PlacementTargetIconResolver.Resolve(new BlockPlacementTarget(blockGuid, null), new BlueprintThumbnailContainer());

            Assert.AreEqual(ForUnitTestModBlockId.BlockId, ((PlacementTargetBlockIcon)icon).BlockId);
        }

        [Test]
        public void 絵の出どころは値で比べる()
        {
            new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            // リストの差し替え判定（D-13）とホットバーの面の比較が作り直しのたびに別物と見なさないよう、値で等しい
            // Equal by value so the list's swap check (D-13) and the hotbar face comparison do not treat each rebuild as new
            Assert.AreEqual(new PlacementTargetBlockIcon(ForUnitTestModBlockId.BlockId), new PlacementTargetBlockIcon(ForUnitTestModBlockId.BlockId));
            var carGuid = Guid.NewGuid();
            Assert.AreEqual(new PlacementTargetTrainCarIcon(carGuid), new PlacementTargetTrainCarIcon(carGuid));
            Assert.AreNotEqual(new PlacementTargetTrainCarIcon(carGuid), new PlacementTargetConnectToolIcon(carGuid));
            Assert.AreNotEqual(PlacementTargetNameOnlyIcon.Instance, new PlacementTargetBlockIcon(ForUnitTestModBlockId.BlockId));
        }

        [Test]
        public void カテゴリはマスタ定義順のGuidを保つ()
        {
            new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var categories = BuildMenuEntryFactory.CreateCategories();

            CollectionAssert.AreEqual(MasterHolder.BuildMenuCategoryMaster.Categories.Select(c => c.CategoryGuid), categories.Select(c => c.CategoryGuid));
            CollectionAssert.AreEqual(MasterHolder.BuildMenuCategoryMaster.Categories[0].SubCategories.Select(s => s.SubCategoryGuid), categories[0].SubCategoryGuids);
        }
    }
}
