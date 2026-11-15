using System;
using Client.Game.InGame.UI.UIState;
using Client.Tests.Ui.Content;
using Client.Tests.Ui.Screens.Inventory.Support;
using Client.Tests.Ui.State.Inventory.Panel;
using Client.Tests.Ui.TestDoubles;
using Client.Ui.Content;
using Client.Ui.Foundation.Input.OsCursor;
using Client.Ui.Foundation.Layers;
using Client.Ui.Foundation.Stage;
using Client.Ui.Screens.Inventory.Equipment;
using Client.Ui.Screens.Inventory.Panel;
using Client.Ui.Screens.Inventory.Parts;
using Client.Ui.State.Inventory.Equipment;
using Client.Ui.State.Inventory.Logic;
using Client.Ui.State.Items;
using Client.Ui.State.Routing;
using Core.Master;
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;
using UniRx;
using UnityEngine;
using UnityEngine.UIElements;

namespace Client.Tests.Ui.Screens.Inventory
{
    public class InventoryEquipmentFlowTest : ScreenTestHost
    {
        private static readonly Guid WoodGuid = Guid.Parse("00000000-0000-0000-1234-000000000001");
        private InventoryScreenFixture _fixture;
        private InventoryView _inventory;
        private EquipmentHudView _equipment;
        private Texture2D _texture;
        private ItemId _wood;

        [SetUp]
        public void SetUp()
        {
            new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            _wood = MasterHolder.ItemMaster.GetItemId(WoodGuid);
            _texture = new Texture2D(4, 4) { hideFlags = HideFlags.DontSave };
            _fixture = new InventoryScreenFixture(36, 3, Toaster);
            var content = new GameContentPresentation(new FakeContentTextureSource(_texture, _texture, _texture));
            var gestures = new PlayerSlotGestureRouter(_fixture.Commands, Toaster);
            var layers = UiLayerStack.Build(rootVisualElement);
            _inventory = new InventoryView(gestures, new RecordingInventorySortCommands(), content, Toaster, new UiOsCursorRequestStack(new FakeUiOsCursorApplier()));
            layers.GetLayer(UiLayer.Screen, UiFamily.Stage).Add(_inventory);
            _equipment = new EquipmentHudView(gestures, content);
            layers.GetLayer(UiLayer.Chrome, UiFamily.Viewport).Add(_equipment);
            _inventory.SetShown(true);
            _equipment.SetShown(true);
            _equipment.SetClicksAccepted(UiScreenTraitsTable.Get(UiScreen.PlayerInventory).Grab);
            _fixture.Model.Current.Subscribe(model =>
            {
                _inventory.Render(model);
                _equipment.Render(model);
            });

            // 描いた直後はヒット判定が前の配置のままなので、もう 1 フレーム進めて確定させる
            // Hit testing still uses the previous layout right after rendering, so advance one more frame to settle it
            simulate.FrameUpdate();
        }

        [TearDown]
        public void TearDown()
        {
            UnityEngine.Object.DestroyImmediate(_texture);
        }

        /// <web-test file="e2e/tests/inventory/equipment.spec.ts">メインのアイテムをUI操作だけで装備スロットへ移せる</web-test>
        [Test]
        public void メインのアイテムを押下だけで装備の枠へ移せる()
        {
            MoveMainOneToEquipmentOne();
            Assert.AreEqual(10, _fixture.Operations.ReadSnapshot().Equipment[1].Count);
            Assert.IsTrue(_fixture.Operations.ReadGrab().IsEmpty, "掴みが空になる");
        }

        /// <web-test file="e2e/tests/inventory/equipment.spec.ts">装備枠のクリックは選択ではなくアイテム移動になる</web-test>
        [Test]
        public void 装備の枠のクリックは選択ではなく移動になる()
        {
            _fixture.Operations.SetEquipment(0, new SlotItem(_wood, 1));
            Republish();

            PressAndRelease(Find<VisualElement>(_equipment, "equipment-slot-0"));

            CollectionAssert.Contains(_fixture.Operations.Moves, "Equipment:0->Grab x1");
            Assert.IsEmpty(_fixture.Equipment.Selections);
        }

        /// <web-test file="e2e/tests/inventory/equipment.spec.ts">装備へ移したアイテムをホイールで選択できる</web-test>
        [Test]
        public void 装備へ移したアイテムをホイールで選べる()
        {
            MoveMainOneToEquipmentOne();
            var host = new GameObject("equipment-wheel");
            new EquipmentWheelRouterFixture(host, UIStateEnum.GameScreen, _fixture.Equipment).Router.Route(100, WheelDeltaUnit.Pixel, false);
            simulate.FrameUpdate();
            UnityEngine.Object.DestroyImmediate(host);

            // 選択の Fake の合図が持ち物の合図へ繋がっているので、HUD の選択枠が移る
            // The selection fake joins the inventory signal, so the HUD's selected frame moves
            Assert.IsTrue(Find<VisualElement>(_equipment, "equipment-slot-1").ClassListContains("slot-frame--selected"));
        }

        private void MoveMainOneToEquipmentOne()
        {
            _fixture.Operations.SetMain(1, new SlotItem(_wood, 10));
            Republish();
            var f = Find<VisualElement>(_inventory, "main-slot-1");
            PressAndRelease(f);
            PressAndRelease(Find<VisualElement>(_equipment, "equipment-slot-1"));
        }

        private void Republish()
        {
            // 合図は流れ済み。レイアウトを進める
            // Already signalled; advance layout
            simulate.FrameUpdate();
        }

        private void PressAndRelease(VisualElement frame)
        {
            simulate.MouseDown(frame.worldBound.center, MouseButton.LeftMouse);
            simulate.MouseUp(frame.worldBound.center, MouseButton.LeftMouse);
            _fixture.Button.Release();
            _fixture.Drag.LateTick();
            _fixture.Button.SetPressed(true);
            simulate.FrameUpdate();
        }
    }
}
