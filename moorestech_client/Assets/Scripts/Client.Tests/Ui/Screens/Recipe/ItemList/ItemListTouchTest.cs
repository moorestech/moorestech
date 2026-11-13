using Client.Ui.Components.Controls;
using Client.Ui.Screens.Recipe.Parts;
using Client.Ui.State.Recipe.Screen;
using Core.Master;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UIElements;

namespace Client.Tests.Ui.Screens.Recipe.ItemList
{
    /// <summary>
    /// タッチでもタップが選択・ドラッグがスクロール（L1）
    /// Verifies that with a touch pointer a tap on the list selects and a drag scrolls (L1, R-11)
    /// </summary>
    /// <remarks>
    /// ScrollView の組み込みのタッチスクロールは閾値を超えた移動で中身の容器へ捕捉を移す。一覧は押下で視口が先に捕捉するので奪われないことを、移動ごとの送り量と捕捉先で見る（S4-recipe の推測の検証）
    /// The ScrollView's built-in touch scrolling moves capture to its content container past its move threshold; the list's viewport captures at press first, so per move the scroll amount and the capture owner show it is never stolen (checks the S4-recipe hypothesis)
    /// </remarks>
    public class ItemListTouchTest : ItemListTestHost
    {
        [Test]
        public void タッチのタップは押したセルのアイテムを選ぶ()
        {
            var view = MountList(ListOf(MaxItemId));
            var at = view.Q<ItemListCellView>("item-list-cell-3").worldBound.center;

            // 4px は閾値 5 未満なのでタップ
            // 4px is under the threshold of 5, so it is a tap
            TouchDown(at);
            TouchMove(at, at + new Vector2(0, 4));
            TouchUp(at + new Vector2(0, 4));
            Assert.IsInstanceOf<ItemRecipeSelection>(Selection.Current.Value);
            Assert.AreEqual(new ItemId(3), ((ItemRecipeSelection)Selection.Current.Value).ItemId);
        }

        [Test]
        public void タッチのドラッグは移動ごとに指に付いてスクロールし選ばない()
        {
            var view = MountList(ListOf(MaxItemId));
            var scroll = view.Q<GameScrollView>("item-list-scroll");
            scroll.SetScrollOffset(100);
            simulate.FrameUpdate();

            // 移動ごとに押下点からの量だけ送られる
            // Each move scrolls by the distance from the press point (built-in touch scrolling would skew it if stacked, or stop it if it stole capture)
            var at = view.Q<ItemListCellView>("item-list-cell-40").worldBound.center;
            TouchDown(at);
            var last = at;
            foreach (var dy in new[] { -10f, -20f, -30f })
            {
                var next = at + new Vector2(0, dy);
                TouchMove(last, next);
                last = next;
                Assert.AreEqual(100f - dy, scroll.scrollOffset.y, 0.5f, $"after moving {dy}");
                // 捕捉は押下で掴んだ視口から移らない
                // Capture stays on the viewport grabbed at press (moving to the ScrollView content container would abort the grab)
                Assert.AreSame(scroll.contentViewport, rootVisualElement.panel.GetCapturingElement(PointerId.touchPointerIdBase), $"capture after moving {dy}");
            }
            TouchUp(last);

            // 離した後も慣性で流れない
            // No inertia after release (same as the Web useDragScroll)
            for (var i = 0; i < 10; i++) simulate.FrameUpdate();
            Assert.AreEqual(130f, scroll.scrollOffset.y, 0.5f);
            Assert.IsInstanceOf<NoRecipeSelection>(Selection.Current.Value);
        }

        // 指 0 の押下・移動・解放をパネルへ送る
        // Send finger 0's press, move and release to the panel (picked by position, or routed to the capturing element)
        private void TouchDown(Vector2 at)
        {
            using (var down = PointerDownEvent.GetPooled(Finger(TouchPhase.Began, at, Vector2.zero), EventModifiers.None)) rootVisualElement.panel.visualTree.SendEvent(down);
            simulate.FrameUpdate();
        }

        private void TouchMove(Vector2 from, Vector2 to)
        {
            using (var move = PointerMoveEvent.GetPooled(Finger(TouchPhase.Moved, to, to - from), EventModifiers.None)) rootVisualElement.panel.visualTree.SendEvent(move);
            simulate.FrameUpdate();
        }

        private void TouchUp(Vector2 at)
        {
            using (var up = PointerUpEvent.GetPooled(Finger(TouchPhase.Ended, at, Vector2.zero), EventModifiers.None)) rootVisualElement.panel.visualTree.SendEvent(up);
            simulate.FrameUpdate();
        }

        private static Touch Finger(TouchPhase phase, Vector2 position, Vector2 delta)
        {
            return new Touch { fingerId = 0, phase = phase, position = position, deltaPosition = delta, tapCount = 1 };
        }
    }
}
