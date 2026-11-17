using Client.Tests.Ui.Screens.GrabOverlay;
using Client.Tests.Ui.TestDoubles;
using Client.Tests.Ui.TestHost;
using Client.Ui.Components.Controls;
using Client.Ui.Foundation.Input.OsCursor;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UIElements;

namespace Client.Tests.Ui.Components.Controls
{
    /// <summary>
    /// IconButton を押せなくして戻す間の指の要求を確かめる（L1）。戻す時点の実位置で決めること・外れた間の再開で持ち越さないこと
    /// Verifies the hand request across IconButton unpressable and restore (L1): decided by the actual position at restore and nothing carried across a detach
    /// </summary>
    public class IconButtonSuspendResumeTest : UiTestHost
    {
        private FakeUiOsCursorApplier _cursorApplier;
        private FakeUiScreenPointer _screenPointer;

        [Test]
        public void 押せなくすると指を外し乗ったまま戻すと指を出し直し離れて戻すと出さない()
        {
            var button = AddButton();
            Hover(button);
            Assert.AreEqual(UiOsCursorShape.Pointer, _cursorApplier.Current, "乗ると指");

            // 押せなくすると指を外し、乗ったまま戻すと出し直す
            // Making it unpressable drops the hand; restoring while hovered re-requests it
            button.SetInteractive(false);
            Assert.AreEqual(UiOsCursorShape.Default, _cursorApplier.Current, "押せなくしても指が残る");
            button.SetInteractive(true);
            Assert.AreEqual(UiOsCursorShape.Pointer, _cursorApplier.Current, "乗ったまま戻しても指が出ない");

            button.SetInteractive(false);
            _screenPointer.SetCurrent(button.worldBound.max + new Vector2(40f, 40f));
            button.SetInteractive(true);
            Assert.AreEqual(UiOsCursorShape.Default, _cursorApplier.Current, "離れてから戻したのに指が出る");
        }

        [Test]
        public void 押せない間に拾わない所へ動いてから戻しても指を出さない()
        {
            // 拾わない間は PointerMove が届かないので、位置だけ動かして事象は流さない
            // While unpickable no PointerMove arrives, so move only the position and send no event
            var button = AddButton();
            Hover(button);
            button.SetInteractive(false);

            _screenPointer.SetCurrent(button.worldBound.max + new Vector2(40f, 40f));
            button.SetInteractive(true);

            Assert.AreEqual(UiOsCursorShape.Default, _cursorApplier.Current, "拾わない所へ動いたのに戻すと指が出る");
        }

        [Test]
        public void 押せない間にパネルから外れた再開は指を出さず載せ直した後は乗ったまま戻すと出す()
        {
            var button = AddButton();
            Hover(button);
            button.SetInteractive(false);

            // 外れた間の再開は位置を測れないので指を出さない
            // Resuming while detached cannot measure the position, so no hand is requested
            button.RemoveFromHierarchy();
            button.SetInteractive(true);
            Assert.AreEqual(UiOsCursorShape.Default, _cursorApplier.Current, "外れた間に戻して指が出る");

            // 載せ直した後の押せなくする・戻すは、外れる前と同じ形で動く
            // After re-attaching, unpressable and restore behave as before the detach
            rootVisualElement.Add(button);
            simulate.FrameUpdate();
            button.SetInteractive(false);
            _screenPointer.SetCurrent(button.worldBound.center);
            button.SetInteractive(true);
            Assert.AreEqual(UiOsCursorShape.Pointer, _cursorApplier.Current, "載せ直した後に乗ったまま戻しても指が出ない");
        }

        [Test]
        public void 押せない間に覆いが被さった所へ戻しても指を出さず覆いが外れた後は出す()
        {
            var button = AddButton();
            Hover(button);
            button.SetInteractive(false);

            // ボタンの全面を覆う拾う要素を重ね、乗ったまま戻しても覆いの下なので指を取らない
            // Cover the whole button with a picking element; restoring while the pointer is over it must not take the hand
            var cover = new VisualElement { pickingMode = PickingMode.Position };
            cover.style.position = Position.Absolute;
            cover.style.left = button.worldBound.xMin - 4f;
            cover.style.top = button.worldBound.yMin - 4f;
            cover.style.width = button.worldBound.width + 8f;
            cover.style.height = button.worldBound.height + 8f;
            rootVisualElement.Add(cover);
            simulate.FrameUpdate();
            button.SetInteractive(true);
            Assert.AreEqual(UiOsCursorShape.Default, _cursorApplier.Current, "覆われているのに戻すと指が出る");

            // 覆いを外してからの押せなくする・戻すは乗ったままなら指を出す
            // After removing the cover, unpressable and restore while hovered request the hand again
            cover.RemoveFromHierarchy();
            simulate.FrameUpdate();
            button.SetInteractive(false);
            button.SetInteractive(true);
            Assert.AreEqual(UiOsCursorShape.Pointer, _cursorApplier.Current, "覆いが外れたのに戻しても指が出ない");
        }

        private IconButton AddButton()
        {
            _cursorApplier = new FakeUiOsCursorApplier();
            _screenPointer = new FakeUiScreenPointer(1f);
            var button = new IconButton("icon-button-suspend-test", new IconButtonCloseGlyph(), new UiOsCursorRequestStack(_cursorApplier, _screenPointer));
            rootVisualElement.Add(button);
            simulate.FrameUpdate();
            return button;
        }

        private void Hover(IconButton button)
        {
            simulate.MouseMove(button.worldBound.center);
            _screenPointer.SetCurrent(button.worldBound.center);
        }
    }
}
