using System;
using System.Collections.Generic;
using System.Linq;
using Client.Game.InGame.BlockSystem.PlaceSystem.Blueprint.Thumbnail;
using Client.Game.InGame.BlockSystem.PlaceSystem.Targets;
using Client.Game.InGame.Construction;
using Client.Localization;
using Client.Tests.PlaceSystem.Blueprint;
using Client.Tests.Ui.State.BuildMenu;
using Client.Ui.State.BuildMenu.Entries;
using Client.Ui.State.BuildMenu.Source;
using Client.Ui.State.PlacementTarget;
using Common.Debug;
using Core.Item.Interface;
using Core.Master;
using Game.Block.Interface;
using Game.Block.Interface.Extension;
using Game.Blueprint;
using Game.Construction;
using Game.Context;
using Game.PlacementTarget;
using Mooresmaster.Model.BuildMenuModule;
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;
using UnityEngine;

namespace Client.Tests.Ui.State.BuildMenu.Source
{
    /// <summary>
    /// 配置対象の列からエントリの列を組む規則を確かめる（L0）
    /// Verifies the rules that turn placement targets into build-menu entries (L0)
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

        private static readonly Dictionary<PlacementTargetKind, string> ExpectedKindNames = new()
        {
            { PlacementTargetKind.Block, "block" },
            { PlacementTargetKind.TrainCar, "trainCar" },
            { PlacementTargetKind.ConnectTool, "connectTool" },
            { PlacementTargetKind.BlueprintCopy, "blueprintCopy" },
            { PlacementTargetKind.Blueprint, "blueprint" },
        };

        [Test]
        public void 全解放の対象を順に1対1でエントリにし種別名とカテゴリ対と表示名を持たせる()
        {
            new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var targets = new PlacementTargetResolver(new PlacementTargetCatalog(new BeltConveyorPlacementUnlockSourceMap()), new BlueprintLookupStub(CreateBlueprint()), new AllPlacementTargetsUnlocked()).CreateUnlockedTargets();

            var entries = BuildMenuEntryFactory.CreateEntries(targets, new ConstructionWalletQuery(new ClientRemainingPlacementCountDatastore()), new List<IItemStack>(), new BlueprintThumbnailContainer());

            // 同じ順に 1 対 1 で並び、各欄が対象どおり
            // Same order one-to-one, each field following its target
            Assert.Greater(entries.Count, 0);
            Assert.AreEqual(targets.Count, entries.Count);
            for (var i = 0; i < targets.Count; i++)
            {
                var target = targets[i];
                var entry = entries[i];
                Assert.AreEqual(target.Id, entry.Catalog.TargetId);
                Assert.AreEqual(ExpectedKindNames[target.Kind], entry.KindName, target.Id.ToString());
                Assert.AreEqual(ExpectedCategoryPair(target), (entry.Catalog.CategoryGuid, entry.Catalog.SubCategoryGuid), target.Id.ToString());
                Assert.AreEqual(target.DisplayName, entry.Catalog.DisplayLabel);
                Assert.IsFalse(entry.Catalog.PaymentWaived, $"{target.Id} is waived without a reason");
            }

            // 5 種がそろう（種別名の表の全行を通る）
            // All five kinds are present, so every row of the kind table is exercised
            CollectionAssert.AreEquivalent(ExpectedKindNames.Values, entries.Select(entry => entry.KindName).Distinct());

            #region Internal

            // ブロックはマスタのカテゴリ名、他は入り口の種別のサブカテゴリ
            // Blocks use their master category names; other kinds use their entry-source sub-category
            (Guid, Guid) ExpectedCategoryPair(IPlacementTarget target)
            {
                var categoryMaster = MasterHolder.BuildMenuCategoryMaster;
                if (target is BlockPlacementTarget block)
                {
                    var blockMaster = MasterHolder.BlockMaster.GetBlockMaster(block.BlockId);
                    return categoryMaster.GetGuidPair(blockMaster.Category, blockMaster.SubCategory);
                }
                var entrySource = target.Kind switch
                {
                    PlacementTargetKind.TrainCar => BuildMenuSubCategoryElement.EntrySourceConst.trainCars,
                    PlacementTargetKind.ConnectTool => BuildMenuSubCategoryElement.EntrySourceConst.connectTools,
                    PlacementTargetKind.BlueprintCopy => BuildMenuSubCategoryElement.EntrySourceConst.blueprintCopyTool,
                    _ => BuildMenuSubCategoryElement.EntrySourceConst.savedBlueprints,
                };
                return categoryMaster.GetPairByEntrySource(entrySource);
            }

            // BP（ブロック2つ）を 1 つ持たせて保存 BP の種別も通す
            // Hold one blueprint (two blocks) so the saved-blueprint kind is exercised too
            BlueprintJsonObject CreateBlueprint()
            {
                var blocks = new List<BlueprintBlockJsonObject>();
                var blockIds = new[] { ForUnitTestModBlockId.GearBeltConveyor, ForUnitTestModBlockId.BlockId };
                for (var i = 0; i < blockIds.Length; i++)
                    blocks.Add(new BlueprintBlockJsonObject(new Vector3Int(i * 10, 0, 0), MasterHolder.BlockMaster.GetBlockMaster(blockIds[i]).BlockGuid.ToString(), (int)BlockDirection.North, new()));
                return new BlueprintJsonObject("starter-base", blocks, new(), new(), Guid.Parse("70000000-0000-4000-8000-000000000001"));
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
        public void 車両と接続ツールはそれぞれのGuidの絵を引く()
        {
            new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var trainCarGuid = MasterHolder.TrainUnitMaster.Train.TrainCars[0].TrainCarGuid;
            var connectToolGuid = MasterHolder.ConnectToolMaster.All[0].ConnectToolGuid;
            var thumbnails = new BlueprintThumbnailContainer();

            Assert.AreEqual(trainCarGuid, ((PlacementTargetTrainCarIcon)PlacementTargetIconResolver.Resolve(new TrainCarPlacementTarget(trainCarGuid), thumbnails)).TrainCarGuid);
            Assert.AreEqual(connectToolGuid, ((PlacementTargetConnectToolIcon)PlacementTargetIconResolver.Resolve(new ConnectToolPlacementTarget(connectToolGuid), thumbnails)).ConnectToolGuid);
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
            var masterCategories = MasterHolder.BuildMenuCategoryMaster.Categories;
            for (var i = 0; i < masterCategories.Length; i++)
                CollectionAssert.AreEqual(masterCategories[i].SubCategories.Select(s => s.SubCategoryGuid), categories[i].SubCategoryGuids, $"category {i}");
        }
    }
}
