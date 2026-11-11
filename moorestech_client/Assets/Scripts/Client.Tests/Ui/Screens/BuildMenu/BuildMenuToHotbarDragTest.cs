using Client.Tests.Ui.Screens.BuildMenu.Support;
using Client.Tests.Ui.Screens.Hotbar;
using Client.Ui.Components.Slots;
using Client.Ui.Foundation.Layers;
using Client.Ui.Foundation.Stage;
using Client.Ui.Screens.Hotbar;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UIElements;

namespace Client.Tests.Ui.Screens.BuildMenu
{
    // hotbar.spec「ビルドメニューエントリを空き枠へドラッグするとhotbar.assign{slot,id}を送る」のパネル越しの通し（S3-hotbar-modal D-16 の残り）
    // Panel-crossing run of the hotbar.spec build-menu-drag case (the remainder of S3-hotbar-modal D-16)
    /// <summary>
    /// ビルドメニューのエントリを本番の層越しにホットバーへ落とす（L1。段階 3 の締め）
    /// Drops a build-menu entry onto the hotbar across the production layers (L1, stage 3 closing)
    /// </summary>
    public class BuildMenuToHotbarDragTest : BuildMenuScreenTestHost
    {
        private RecordingHotbarCommands _hotbarCommands;
        private HotbarHudView _hotbar;

        [SetUp]
        public void PlaceOnProductionLayers()
        {
            // 本番と同じ層と族へメニューとホットバーを載せる
            // Put the menu and the hotbar on the same layers and families as production
            var layers = UiLayerStack.Build(rootVisualElement);
            View.RemoveFromHierarchy();
            layers.GetLayer(UiLayer.Screen, UiFamily.Stage).Add(View);
            _hotbarCommands = new RecordingHotbarCommands();
            _hotbar = new HotbarHudView(_hotbarCommands, HotbarTestPresentation.Create(), Toaster);
            layers.GetLayer(UiLayer.Chrome, UiFamily.Viewport).Add(_hotbar);
            _hotbar.Render(HotbarViewModelSamples.WebFixture());
            OpenMenu(BuildMenuSamples.Create());
        }

        [Test]
        public void エントリを枠1へドラッグすると割当が1回だけ出て選択は出ない()
        {
            var from = Entry("block", BuildMenuSamples.WoodChest).worldBound.center;
            var to = Slot(1).worldBound.center;
            Assert.AreEqual(Slot(1), FirstSlotAncestorOrSelf(panel.Pick(to)), "枠 1 の中心がメニューに覆われていない");

            // 5px の閾値を越えてから枠へ運ぶ
            // Cross the 5px threshold, then carry it to the slot
            simulate.MouseDown(from, MouseButton.LeftMouse);
            simulate.MouseMove(from + new Vector2(8f, -8f));
            simulate.MouseMove(Vector2.Lerp(from, to, 0.5f));
            simulate.MouseMove(to);
            simulate.MouseUp(to, MouseButton.LeftMouse);
            Settle();

            CollectionAssert.AreEqual(new[] { $"assign:1:{BuildMenuSamples.WoodChest:D}" }, _hotbarCommands.Calls);
            CollectionAssert.IsEmpty(Commands.Selected);
        }

        [Test]
        public void 閾値未満で離すとタップの選択だけが出る()
        {
            var from = Entry("block", BuildMenuSamples.WoodChest).worldBound.center;
            simulate.MouseDown(from, MouseButton.LeftMouse);
            simulate.MouseMove(from + new Vector2(3f, 0f));
            simulate.MouseUp(from + new Vector2(3f, 0f), MouseButton.LeftMouse);
            Settle();

            CollectionAssert.AreEqual(new[] { BuildMenuSamples.WoodChest }, Commands.Selected);
            CollectionAssert.IsEmpty(_hotbarCommands.Calls);
        }

        private SlotFrame Slot(int index)
        {
            return Find<SlotFrame>(_hotbar, $"hotbar-slot-{index}");
        }

        private static SlotFrame FirstSlotAncestorOrSelf(VisualElement picked)
        {
            for (var e = picked; e != null; e = e.parent)
                if (e is SlotFrame frame) return frame;
            return null;
        }
    }
}
