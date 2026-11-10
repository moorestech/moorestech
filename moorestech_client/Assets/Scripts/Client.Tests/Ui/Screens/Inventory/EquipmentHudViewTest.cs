using System.Linq;
using Client.Game.InGame.Tutorial.UIHighlight;
using Client.Tests.Ui.Content;
using Client.Tests.Ui.Screens.Inventory.Support;
using Client.Ui.Content;
using Client.Ui.Foundation.Anchor;
using Client.Ui.Foundation.Layers;
using Client.Ui.Foundation.Stage;
using Client.Ui.Screens.Inventory.Equipment;
using Client.Ui.Screens.Inventory.Parts;
using Client.Ui.State.Inventory.Panel;
using Client.Ui.State.Items;
using Client.Ui.State.Routing;
using Core.Master;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UIElements;

namespace Client.Tests.Ui.Screens.Inventory
{
    public class EquipmentHudViewTest : ScreenTestHost
    {
        private RecordingPlayerSlotCommands _commands;
        private EquipmentHudView _view;
        private Texture2D _texture;

        [SetUp]
        public void SetUp()
        {
            _texture = new Texture2D(4, 4) { hideFlags = HideFlags.DontSave };
            _commands = new RecordingPlayerSlotCommands();
            _view = new EquipmentHudView(new PlayerSlotGestureRouter(_commands, Toaster), new GameContentPresentation(new FakeContentTextureSource(_texture, _texture, _texture)));
            UiLayerStack.Build(rootVisualElement).GetLayer(UiLayer.Chrome, UiFamily.Viewport).Add(_view);
            _view.SetShown(true);
        }

        [TearDown]
        public void TearDown()
        {
            UnityEngine.Object.DestroyImmediate(_texture);
        }

        /// <web-test file="src/features/inventory/EquipmentPanel/index.test.ts">装備枠がアンカーを名乗り、選択中の枠には選択アンカーも付く</web-test>
        [Test]
        public void 装備枠がアンカーを名乗り選択枠は選択アンカーも持つ()
        {
            _view.Render(Model(2, 1));

            // Web の期待 ["equipment.hud","equipment.slot-0","equipment.slot-1 equipment.selected-slot"] と同じ並び
            // Same order as the Web expectation ["equipment.hud","equipment.slot-0","equipment.slot-1 equipment.selected-slot"]
            var anchors = _view.Query(className: TutorialAnchor.MarkerClassName).ToList()
                .Select(element => string.Join(" ", element.GetClasses().Where(name => name.StartsWith("equipment."))))
                .ToArray();
            CollectionAssert.AreEqual(new[] { AnchorIds.EquipmentHud, AnchorIds.EquipmentSlot(0), $"{AnchorIds.EquipmentSlot(1)} {AnchorIds.EquipmentSelectedSlot}" }, anchors);
            Assert.IsTrue(Find<VisualElement>(_view, "equipment-slot-1").ClassListContains("slot-frame--selected"));
        }

        /// <web-test file="src/features/inventory/EquipmentPanel/index.test.ts">pauseMenu 中の左押下はハンドラごと無く、アクションも飛ばない</web-test>
        [Test]
        public void ポーズ中の左押下はコマンドを呼ばない()
        {
            PressLeftOnSlotZeroIn(UiScreen.PauseMenu);
            Assert.IsEmpty(_commands.Calls);
        }

        /// <web-test file="src/features/inventory/EquipmentPanel/index.test.ts">GameScreen 中も同様にクリックを受けない</web-test>
        [Test]
        public void ゲーム画面の左押下もコマンドを呼ばない()
        {
            PressLeftOnSlotZeroIn(UiScreen.None);
            Assert.IsEmpty(_commands.Calls);
        }

        /// <web-test file="src/features/inventory/EquipmentPanel/index.test.ts">持ち物画面では左押下が掴み取りを送る</web-test>
        [Test]
        public void 持ち物画面の左押下はプレイヤーの枠のコマンドへ渡る()
        {
            PressLeftOnSlotZeroIn(UiScreen.PlayerInventory);
            CollectionAssert.AreEqual(new[] { "LeftDown Equipment:0 shift=False" }, _commands.Calls);
        }

        /// <web-test file="e2e/tests/inventory/equipment.spec.ts">装備枠0でも装備HUDは面積を保ちアンカーが解決する</web-test>
        [Test]
        public void 装備枠が0でもHUDは面積を持ちアンカーを名乗る()
        {
            _view.Render(Model(0, 0));
            simulate.FrameUpdate();

            Assert.IsTrue(_view.ClassListContains(AnchorIds.EquipmentHud));
            Assert.Greater(_view.worldBound.width, 0f);
            Assert.Greater(_view.worldBound.height, 0f);
        }

        private void PressLeftOnSlotZeroIn(UiScreen screen)
        {
            // クリックを受けるのは掴める画面だけ（Web useGrabInteractive。本番は EquipmentHudPresenter が同じ traits を読む）
            // Clicks are accepted only on grab screens (Web useGrabInteractive; in production EquipmentHudPresenter reads the same traits)
            _view.SetClicksAccepted(UiScreenTraitsTable.Get(screen).Grab);
            _view.Render(Model(2, 0));
            simulate.FrameUpdate();
            var frame = Find<VisualElement>(_view, "equipment-slot-0");
            simulate.MouseDown(frame.worldBound.center, MouseButton.LeftMouse);
            simulate.MouseUp(frame.worldBound.center, MouseButton.LeftMouse);
        }

        private static PlayerInventoryViewModel Model(int equipmentSlots, int selected)
        {
            var empty = new SlotItem(ItemMaster.EmptyItemId, 0);
            var equipment = Enumerable.Repeat(empty, equipmentSlots).ToArray();
            return new PlayerInventoryViewModel(new[] { empty }, equipment, selected, new System.Collections.Generic.Dictionary<int, string>());
        }
    }
}
