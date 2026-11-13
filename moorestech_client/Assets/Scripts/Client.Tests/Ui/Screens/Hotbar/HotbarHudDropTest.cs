using System;
using System.Globalization;
using Client.Tests.Ui.Components;
using Client.Tests.Ui.Screens.BuildMenu.Support;
using Client.Ui.Components.Slots;
using Client.Ui.Foundation.DragDrop;
using Client.Ui.Screens.BuildMenu.Parts;
using Client.Ui.Screens.Hotbar;
using Client.Ui.Screens.Hotbar.Parts;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UIElements;

namespace Client.Tests.Ui.Screens.Hotbar
{
    /// <summary>
    /// 落とし先の解き方と未解決の枠（L1）
    /// How drops resolve and the unresolved slot (L1)
    /// </summary>
    public class HotbarHudDropTest : ScreenTestHost
    {
        private static readonly Guid EntryId = new("7a1e2d3c-4b5a-4968-8776-655443322110");

        private RecordingHotbarCommands _commands;
        private HotbarHudView _view;

        [SetUp]
        public void MountHotbar()
        {
            _commands = new RecordingHotbarCommands();
            _view = Mount(new HotbarHudView(_commands, HotbarTestPresentation.Create(), Toaster));
            _view.Render(HotbarViewModelSamples.WebFixture());
            simulate.FrameUpdate();
            simulate.FrameUpdate();
        }

        /// <web-test file="e2e/tests/hotbar/hotbar.spec.ts">ビルドメニューエントリを空き枠へドラッグするとhotbar.assign{slot,id}を送る</web-test>
        [Test]
        public void ビルドメニューの源を空き枠へ落とすと割当を送りタップは出さない()
        {
            // 画面の左上に本物のエントリの源を単独で置く（パネル越しの結線は S3-ladder。計画 D-16）
            // Put the real entry source alone at the top-left (wiring through the panel is S3-ladder; plan D-16)
            var entry = new VisualElement { name = "build-menu-entry-alone" };
            entry.style.position = Position.Absolute;
            entry.style.left = 10;
            entry.style.top = 10;
            entry.style.width = 60;
            entry.style.height = 60;
            rootVisualElement.Add(entry);
            var entryHandler = new RecordingBuildMenuEntryHandler();
            UiDragDrop.MarkSource(entry, new BuildMenuEntryDragPayload(EntryId, entryHandler));
            simulate.FrameUpdate();

            Drag(entry.worldBound.center, Slot(1).worldBound.center);
            CollectionAssert.AreEqual(new[] { $"assign:1:{EntryId:D}" }, _commands.Calls);
            CollectionAssert.IsEmpty(entryHandler.Selected);
        }

        /// <web-test file="e2e/tests/hotbar/hotbar.spec.ts">未解決の割当枠は使用不可表示になり、枠外ドラッグで外せる</web-test>
        [Test]
        public void 未解決の割当枠は白面のまま減光され枠外へ落とすと解除を送る()
        {
            var slot4 = Slot(4);
            var cell4 = slot4.GetFirstAncestorOfType<HotbarCell>();
            Assert.IsTrue(cell4.ClassListContains("hotbar-hud__cell--unresolved"));
            Assert.IsTrue(slot4.ClassListContains("slot-frame--filled"));
            Assert.IsNull(slot4.Q<PlacementTargetFace>());
            Assert.AreEqual(float.Parse(ThemeTokenFile.Resolve("--hotbar-unresolved-opacity"), CultureInfo.InvariantCulture), GroupOpacity.Read(cell4), 0.001f);

            Drag(slot4.worldBound.center, new Vector2(slot4.worldBound.center.x, 5f));
            CollectionAssert.AreEqual(new[] { "clear:4" }, _commands.Calls);
        }

        /// <web-test file="e2e/tests/hotbar/hotbar.spec.ts">枠外へドラッグするとhotbar.clear{slot}を送る</web-test>
        [Test]
        public void 割当済みの枠を画面の上端へ落とすと解除を送る()
        {
            var slot0 = Slot(0);
            Drag(slot0.worldBound.center, new Vector2(slot0.worldBound.center.x, 5f));
            CollectionAssert.AreEqual(new[] { "clear:0" }, _commands.Calls);
        }

        [Test]
        public void 枠の間の隙間で離すと何も送らない()
        {
            var gap = new Vector2(Slot(1).worldBound.xMax + 2.5f, Slot(1).worldBound.center.y);
            Drag(Slot(0).worldBound.center, gap);
            CollectionAssert.IsEmpty(_commands.Calls);
        }

        private SlotFrame Slot(int index)
        {
            return Find<SlotFrame>(_view, $"hotbar-slot-{index}");
        }

        private void Drag(Vector2 from, Vector2 to)
        {
            simulate.MouseDown(from, MouseButton.LeftMouse);
            simulate.MouseMove(Vector2.Lerp(from, to, 0.5f));
            simulate.MouseMove(to);
            simulate.MouseUp(to, MouseButton.LeftMouse);
            simulate.FrameUpdate();
        }
    }
}
