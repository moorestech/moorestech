using System;
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
using Core.Item.Interface;
using Core.Master;
using Game.Block.Interface.Extension;
using Game.Blueprint;
using Game.Construction;
using Game.PlacementTarget;
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;
using UnityEngine;

namespace Client.Tests.Ui.State.BuildMenu
{
    /// <summary>
    /// ビルドメニューのエントリ組み立てを Web の橋と突き合わせる
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
        }

        [Test]
        public void 全解放の対象でWebの橋と同じ種別とカテゴリ対と素材と免除と1セット設置を出す()
        {
            new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var blueprint = new BlueprintJsonObject("starter-base", new(), new(), new(), Guid.Parse("70000000-0000-4000-8000-000000000001"));
            var targets = new PlacementTargetResolver(new PlacementTargetCatalog(new BeltConveyorPlacementUnlockSourceMap()), new BlueprintLookupStub(blueprint), new AllPlacementTargetsUnlocked()).CreateUnlockedTargets();
            var wallet = new ConstructionWalletQuery(new ClientRemainingPlacementCountDatastore());
            var thumbnails = new BlueprintThumbnailContainer();

            var entries = BuildMenuEntryFactory.CreateEntries(targets, wallet, Array.Empty<IItemStack>(), thumbnails);
            var dtos = BuildMenuEntryDtoFactory.CreateDtos(targets, wallet, Array.Empty<IItemStack>(), thumbnails);

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
                CollectionAssert.AreEqual(dto.RequiredItems.Select(item => (item.ItemId, item.Count, item.Held, item.Lacking)), entry.Materials.Select(row => (row.ItemId.AsPrimitive(), row.Item.Count, row.Item.Held, row.Item.Lacking)), dto.Id);
            }
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
