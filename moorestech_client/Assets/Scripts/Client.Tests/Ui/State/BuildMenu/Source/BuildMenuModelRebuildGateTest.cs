using System;
using Client.Game.InGame.BlockSystem.PlaceSystem.Blueprint;
using Client.Game.InGame.BlockSystem.PlaceSystem.Blueprint.Thumbnail;
using Client.Game.InGame.BlockSystem.PlaceSystem.Targets;
using Client.Game.InGame.Construction;
using Client.Game.InGame.UI.Inventory.Equipment;
using Client.Game.InGame.UI.Inventory.Main;
using Client.Game.InGame.UI.UIState;
using Client.Tests.Common;
using Client.Tests.Ui.Screens.Inventory.Support;
using Client.Ui.State.BuildMenu.Source;
using Client.Ui.State.Routing;
using Core.Master;
using Game.Block.Interface.Extension;
using Game.Construction;
using Game.PlacementTarget;
using Game.Context;
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;
using UniRx;
using UnityEngine;

namespace Client.Tests.Ui.State.BuildMenu.Source
{
    /// <summary>
    /// 所持の変化はビルドメニューを開いている間だけ組み直しを予約することを確かめる（L0）
    /// Verifies inventory changes schedule a rebuild only while the build menu is open (L0)
    /// </summary>
    public class BuildMenuModelRebuildGateTest
    {
        private const int MainSlot = 3;
        private static readonly Guid HeldItemGuid = Guid.Parse("00000000-0000-0000-1234-000000000001");

        private GameObject _controlObject;
        private UiRoutingState _routing;
        private LocalPlayerInventoryController _controller;
        private BuildMenuModel _model;

        [SetUp]
        public void SetUp()
        {
            new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            _controlObject = new GameObject("BuildMenuModelRebuildGateTest.Control");
            _routing = UiRoutingStateTestFactory.Create(_controlObject, UIStateEnum.GameScreen);
            var blueprintLibrary = new ClientBlueprintLibrary();
            var resolver = new PlacementTargetResolver(new PlacementTargetCatalog(new BeltConveyorPlacementUnlockSourceMap()), blueprintLibrary, new AllPlacementTargetsUnlocked());
            _controller = new LocalPlayerInventoryController(new LocalPlayerInventory(), new LocalPlayerEquipment());
            _model = new BuildMenuModel(_routing, resolver, new ConstructionWalletQuery(new ClientRemainingPlacementCountDatastore()), _controller, blueprintLibrary, new BlueprintThumbnailContainer());
            _model.Initialize();
        }

        [TearDown]
        public void TearDown()
        {
            // 購読はゲーム寿命で残るので閉じた状態にしてから片付ける
            // Subscriptions outlive the test, so leave the state closed before cleanup
            SetScreenWithoutNotify(UiScreen.None);
            UnityEngine.Object.DestroyImmediate(_controlObject);
        }

        [Test]
        public void ビルドメニューが閉じている間の所持変化は組み直しを予約しない()
        {
            _controller.SetMainItem(MainSlot, ServerContext.ItemStackFactory.Create(MasterHolder.ItemMaster.GetItemId(HeldItemGuid), 1));

            Assert.IsFalse(TestReflection.GetField<bool>(_model, "_rebuildScheduled"));
        }

        [Test]
        public void ビルドメニューを開いている間の所持変化は組み直しを予約する()
        {
            // 開く遷移は BP ライブラリのサーバー往復を伴うので、画面の値だけを差して所持の分岐を見る
            // Opening round-trips the blueprint library, so only the screen value is set to isolate the inventory branch
            SetScreenWithoutNotify(UiScreen.BuildMenu);
            _controller.SetMainItem(MainSlot, ServerContext.ItemStackFactory.Create(MasterHolder.ItemMaster.GetItemId(HeldItemGuid), 1));

            Assert.IsTrue(TestReflection.GetField<bool>(_model, "_rebuildScheduled"));
        }

        private void SetScreenWithoutNotify(UiScreen screen)
        {
            var current = TestReflection.GetField<ReactiveProperty<UiRoutingSnapshot>>(_routing, "_current");
            TestReflection.SetField(current, "value", new UiRoutingSnapshot(screen));
        }
    }
}
