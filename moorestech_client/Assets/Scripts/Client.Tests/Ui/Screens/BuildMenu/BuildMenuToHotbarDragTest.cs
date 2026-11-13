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
    // エントリの D&D がホットバーへ届く通し（D-16）
    // Entry drags reach the hotbar Drop end to end (D-16)
    public class BuildMenuToHotbarDragTest : BuildMenuScreenTestHost
    {
        private HotbarHudView _hotbar;

        [SetUp]
        public void PlaceOnProductionLayers()
        {
            // 本番と同じ層・族へ載せる
            // Mount on the same layers and families as production
            var layers = UiLayerStack.Build(rootVisualElement);
            View.RemoveFromHierarchy();
            layers.GetLayer(UiLayer.Screen, UiFamily.Stage).Add(View);
            _hotbar = new HotbarHudView(HotbarCommands, HotbarTestPresentation.Create(), Toaster);
            layers.GetLayer(HotbarHudPresenter.HostLayer, HotbarHudPresenter.HostFamily).Add(_hotbar);
            _hotbar.Render(HotbarViewModelSamples.WebFixture());
            OpenMenu(BuildMenuSamples.Create());
        }

        [Test]
        public void エントリを枠1へドラッグすると枠1への落としが1回だけ出て選択は出ない()
        {
            var from = Entry("block", BuildMenuSamples.WoodChest).worldBound.center;
            var to = Slot(1).worldBound.center;
            Assert.AreEqual(Slot(1), FirstSlotAncestorOrSelf(panel.Pick(to)), "枠 1 の中心がメニューに覆われていない");

            // 5px の閾値を越えてから枠へ運ぶ
            // Cross the 5px threshold, then carry it to the slot
            DragPastThreshold(from, to);

            CollectionAssert.AreEqual(new[] { $"entry:{BuildMenuSamples.WoodChest:D}->slot:1" }, HotbarCommands.Calls);
            CollectionAssert.IsEmpty(Commands.Selected);
        }

        // 枠外も Drop を通る（D-11）
        // Outside drops also pass Drop (D-11)
        [Test]
        public void エントリをメニューの見出しへドラッグすると枠外への落としが1回だけ出て選択は出ない()
        {
            var from = Entry("block", BuildMenuSamples.WoodChest).worldBound.center;
            var to = Find<VisualElement>(View, $"build-menu-category-heading-{BuildMenuSamples.Logistics:D}").worldBound.center;
            DragPastThreshold(from, to);

            CollectionAssert.AreEqual(new[] { $"entry:{BuildMenuSamples.WoodChest:D}->outside" }, HotbarCommands.Calls);
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
            CollectionAssert.IsEmpty(HotbarCommands.Calls);
        }

        // 5px の閾値を越えてから運んで離す
        // Cross the 5px threshold, carry it over and release
        private void DragPastThreshold(Vector2 from, Vector2 to)
        {
            simulate.MouseDown(from, MouseButton.LeftMouse);
            simulate.MouseMove(from + new Vector2(8f, -8f));
            simulate.MouseMove(Vector2.Lerp(from, to, 0.5f));
            simulate.MouseMove(to);
            simulate.MouseUp(to, MouseButton.LeftMouse);
            Settle();
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
