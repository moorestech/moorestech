using System;
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
    /// 枠とビルドメニューの源の押下をタップとドラッグに分ける（L1。Web useHotbarDragSource.test.ts）
    /// Splitting presses on slots and the build-menu source into taps and drags (L1; Web useHotbarDragSource.test.ts)
    /// </summary>
    public class HotbarSlotDragGestureTest : ScreenTestHost
    {
        private static readonly Guid EntryId = new("7a1e2d3c-4b5a-4968-8776-655443322110");

        private RecordingHotbarCommands _commands;
        private HotbarHudView _view;
        private VisualElement _entry;
        private RecordingBuildMenuEntryHandler _entryHandler;

        [SetUp]
        public void MountHotbarAndEntry()
        {
            _commands = new RecordingHotbarCommands();
            _view = Mount(new HotbarHudView(_commands, HotbarTestPresentation.Create(), Toaster));
            _view.Render(HotbarViewModelSamples.WebFixture());

            // 左上にエントリの源を単独で置く
            // The entry source alone at the top-left
            _entry = new VisualElement { name = "build-menu-entry-alone" };
            _entry.style.position = Position.Absolute;
            _entry.style.left = 10;
            _entry.style.top = 10;
            _entry.style.width = 60;
            _entry.style.height = 60;
            rootVisualElement.Add(_entry);
            _entryHandler = new RecordingBuildMenuEntryHandler();
            UiDragDrop.MarkSource(_entry, new BuildMenuEntryDragPayload(EntryId, _entryHandler));
            simulate.FrameUpdate();
            simulate.FrameUpdate();
        }

        /// <web-test file="src/features/hotbar/useHotbarDragSource.test.ts">閾値未満で離すとタップとしてonTapが1回呼ばれる(ビルドメニュー/ホットバー枠どちらのクリック選択も壊れない)</web-test>
        [Test]
        public void 閾値未満で離すと枠もエントリも1回のタップになる()
        {
            var slot2 = Slot(2).worldBound.center;
            Press(slot2, slot2 + new Vector2(3f, 0f));
            Press(_entry.worldBound.center, _entry.worldBound.center + new Vector2(3f, 0f));
            CollectionAssert.AreEqual(new[] { "select:2" }, _commands.Calls);
            CollectionAssert.AreEqual(new[] { EntryId }, _entryHandler.Selected);
        }

        /// <web-test file="src/features/hotbar/useHotbarDragSource.test.ts">閾値を超えるとドラッグへ確定しonTapは発火しない</web-test>
        [Test]
        public void 閾値を超えるとドラッグになり自分の枠で離しても選択しない()
        {
            var slot0 = Slot(0).worldBound.center;
            Press(slot0, slot0 + new Vector2(20f, 0f));
            CollectionAssert.AreEqual(new[] { "none" }, _commands.Calls);
        }

        /// <web-test file="src/features/hotbar/useHotbarDragSource.test.ts">枠外(elementFromPointが枠を返さない)へドラッグして離すとhotbar.clearをdispatchする</web-test>
        [Test]
        public void 枠の無い所へ落とすと解除する()
        {
            Press(Slot(0).worldBound.center, new Vector2(640f, 100f));
            CollectionAssert.AreEqual(new[] { "clear:0" }, _commands.Calls);
        }

        /// <web-test file="src/features/hotbar/useHotbarDragSource.test.ts">data-hotbar-slot-index付きの枠へドラッグして離すとhotbar.swapをdispatchする</web-test>
        [Test]
        public void 別の枠や番号タブの上へ落とすと入れ替える()
        {
            Press(Slot(0).worldBound.center, Slot(2).worldBound.center);
            var tab = Slot(6).parent.Q(className: HotbarNumberTab.ClassName);
            Press(Slot(0).worldBound.center, tab.worldBound.center);
            CollectionAssert.AreEqual(new[] { "swap:0:2", "swap:0:6" }, _commands.Calls);
        }

        /// <web-test file="src/features/hotbar/useHotbarDragSource.test.ts">ビルドメニューエントリを枠へドラッグして離すとhotbar.assignをdispatchする</web-test>
        [Test]
        public void エントリを枠へ落とすと割当てる()
        {
            Press(_entry.worldBound.center, Slot(5).worldBound.center);
            CollectionAssert.AreEqual(new[] { $"assign:5:{EntryId:D}" }, _commands.Calls);
            CollectionAssert.IsEmpty(_entryHandler.Selected);
        }

        /// <web-test file="src/features/hotbar/useHotbarDragSource.test.ts">空枠を閾値超えで引きずって離してもタップにならない（建築モードを抜けさせない）</web-test>
        [Test]
        public void 空き枠を引きずって離しても選択も解除もしない()
        {
            Press(Slot(1).worldBound.center, new Vector2(640f, 100f));
            CollectionAssert.IsEmpty(_commands.Calls);
        }

        /// <web-test file="src/features/hotbar/useHotbarDragSource.test.ts">右ボタン押下は無視し、preventDefaultもpointer捕捉も行わない（右クリック削除の温存）</web-test>
        [Test]
        public void 右ボタンの押下は捕捉せず何も送らない()
        {
            var slot0 = Slot(0);
            simulate.MouseDown(slot0.worldBound.center, MouseButton.RightMouse);
            Assert.IsFalse(slot0.HasPointerCapture(PointerId.mousePointerId));
            simulate.MouseUp(slot0.worldBound.center, MouseButton.RightMouse);
            simulate.FrameUpdate();
            CollectionAssert.IsEmpty(_commands.Calls);
        }

        private SlotFrame Slot(int index)
        {
            return Find<SlotFrame>(_view, $"hotbar-slot-{index}");
        }

        private void Press(Vector2 from, Vector2 to)
        {
            simulate.MouseDown(from, MouseButton.LeftMouse);
            simulate.MouseMove(Vector2.Lerp(from, to, 0.5f));
            simulate.MouseMove(to);
            simulate.MouseUp(to, MouseButton.LeftMouse);
            simulate.FrameUpdate();
        }
    }
}
