using System;
using System.Collections;
using System.Linq;
using Client.Game.InGame.BlockSystem.PlaceSystem.Blueprint;
using Client.Game.InGame.BlockSystem.PlaceSystem.Blueprint.Thumbnail;
using Client.Game.InGame.BlockSystem.PlaceSystem.Targets;
using Client.Game.InGame.Construction;
using Client.Game.InGame.UI.Inventory.Equipment;
using Client.Game.InGame.UI.Inventory.Main;
using Client.Game.InGame.UI.UIState;
using Client.Localization;
using Client.Tests.Common;
using Client.Ui.State.BuildMenu.Entries;
using Client.Ui.State.BuildMenu.Source;
using Core.Item.Interface;
using Core.Master;
using Game.Block.Interface.Extension;
using Game.Construction;
using Game.Context;
using Game.PlacementTarget;
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;
using UnityEngine;
using UnityEngine.TestTools;

namespace Client.Tests.Ui.State.BuildMenu
{
    /// <summary>
    /// BuildMenuModel が開いている間の源の変化で組み直し、並び・検索・所持を Current に載せることを確かめる（bd albof.57.3）
    /// Verifies BuildMenuModel rebuilds on source changes while open and publishes order, searchable labels and held counts through Current (bd albof.57.3)
    /// </summary>
    public class BuildMenuModelTest
    {
        private const string CurrentStateField = "<CurrentState>k__BackingField";
        private const int MaxFramesToRebuild = 30;

        private GameObject _controlObject;
        private UIStateControl _control;
        private LocalPlayerInventoryController _inventory;
        private PlacementTargetResolver _resolver;
        private BuildMenuModel _model;

        [SetUp]
        public void SetUp()
        {
            // 表示名は辞書を引くので英語で組む（前例 BuildMenuEntryFactoryTest）
            // Display names read the dictionary, so compose in English (precedent BuildMenuEntryFactoryTest)
            new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            Localize.Initialize();
            Localize.TrySetChosenLanguage("english");

            // 開く遷移は BP ライブラリのサーバー往復を伴うので通さず、状態だけを差して源の変化の経路を見る（前例 BuildMenuTopicRepublishTest）
            // Entering the menu round-trips the blueprint library to the server, so only the state is set to exercise the source-change path (precedent BuildMenuTopicRepublishTest)
            _controlObject = new GameObject("BuildMenuModelTest.Control");
            _control = _controlObject.AddComponent<UIStateControl>();
            var blueprintLibrary = new ClientBlueprintLibrary();
            _resolver = new PlacementTargetResolver(new PlacementTargetCatalog(new BeltConveyorPlacementUnlockSourceMap()), blueprintLibrary, new AllPlacementTargetsUnlocked());
            _inventory = new LocalPlayerInventoryController(new LocalPlayerInventory(), new LocalPlayerEquipment());
            var wallet = new ConstructionWalletQuery(new ClientRemainingPlacementCountDatastore());
            _model = new BuildMenuModel(_control, _resolver, wallet, _inventory, blueprintLibrary, new BlueprintThumbnailContainer());
            _model.Initialize();
        }

        [TearDown]
        public void TearDown()
        {
            // Model の購読はゲーム寿命で残るので、閉じた状態にして後のテストで組み直さないようにする
            // The model's subscriptions live for the game, so leave the state closed to keep later tests from rebuilding
            TestReflection.SetField(_control, CurrentStateField, UIStateEnum.GameScreen);
            UnityEngine.Object.DestroyImmediate(_controlObject);
            Localize.Initialize();
        }

        [UnityTest]
        public IEnumerator 並びは解放済みの配置対象の順でカテゴリはマスタ順()
        {
            yield return OpenAndHold(0, FirstMaterialItemId(), 1);

            var current = _model.Current.Value;
            CollectionAssert.AreEqual(_resolver.CreateUnlockedTargets().Select(target => target.Id), current.CatalogEntries.Select(entry => entry.TargetId));
            CollectionAssert.AreEqual(BuildMenuEntryFactory.CreateCategories().Select(category => category.CategoryGuid), current.Categories.Select(category => category.CategoryGuid));
        }

        [UnityTest]
        public IEnumerator 検索は組み直した表示名で絞る()
        {
            yield return OpenAndHold(0, FirstMaterialItemId(), 1);

            // 先頭のエントリの表示名で引くと、そのエントリが残り、残る全部がその語を含む
            // Searching by the first entry's label keeps that entry, and every kept entry contains the word
            var current = _model.Current.Value;
            var first = current.CatalogEntries[0];
            var shown = BuildMenuShownListComposer.Compose(current, first.DisplayLabel).Groups.SelectMany(group => group.Sections).SelectMany(section => section.Entries).ToList();
            Assert.IsTrue(shown.Any(entry => entry.TargetId == first.TargetId), first.DisplayLabel);
            Assert.IsTrue(shown.All(entry => entry.DisplayLabel.ToLowerInvariant().Contains(first.DisplayLabel.ToLowerInvariant())), first.DisplayLabel);
            Assert.Less(shown.Count, current.CatalogEntries.Count, "検索語が全部に当たり絞り込みを確かめられない");
        }

        [UnityTest]
        public IEnumerator 所持の変化は開いている間だけ所持数に載る()
        {
            // 閉じている間の変化は組み直さない
            // Changes while closed do not rebuild
            var material = FirstMaterialItemId();
            _inventory.SetMainItem(0, ServerContext.ItemStackFactory.Create(material, 2));
            for (var frame = 0; frame < 5; frame++) yield return null;
            Assert.AreSame(BuildMenuViewModel.Empty, _model.Current.Value);

            // 開いている間の変化は、閉じている間の分も合わせて、その素材を使う全行の所持数に載る
            // A change while open puts the total, including the closed-time change, on every row using that material
            yield return OpenAndHold(1, material, 3);
            var current = _model.Current.Value;
            var lines = current.CatalogEntries.Select(entry => current.TryGetEntry(entry.TargetId, out var model) ? model : null)
                .SelectMany(model => model.Materials).Where(line => line.Row.ItemId == material).ToList();
            Assert.Greater(lines.Count, 0);
            Assert.IsTrue(lines.All(line => line.Row.Item.Held == 5), string.Join(",", lines.Select(line => line.Row.Item.Held)));
        }

        // 開いた状態で所持品を変え、フレーム末の組み直しを待つ
        // Open, change the inventory, and wait for the end-of-frame rebuild
        private IEnumerator OpenAndHold(int slot, ItemId itemId, int count)
        {
            TestReflection.SetField(_control, CurrentStateField, UIStateEnum.BuildMenu);
            var before = _model.Current.Value;
            _inventory.SetMainItem(slot, ServerContext.ItemStackFactory.Create(itemId, count));
            for (var frame = 0; frame < MaxFramesToRebuild && ReferenceEquals(before, _model.Current.Value); frame++) yield return null;
            Assert.AreNotSame(before, _model.Current.Value, "開いている間の所持の変化で組み直されない");
        }

        private ItemId FirstMaterialItemId()
        {
            var probe = BuildMenuEntryFactory.CreateEntries(_resolver.CreateUnlockedTargets(), new ConstructionWalletQuery(new ClientRemainingPlacementCountDatastore()), Array.Empty<IItemStack>(), new BlueprintThumbnailContainer());
            return probe.SelectMany(entry => entry.Materials).First().Row.ItemId;
        }
    }
}
