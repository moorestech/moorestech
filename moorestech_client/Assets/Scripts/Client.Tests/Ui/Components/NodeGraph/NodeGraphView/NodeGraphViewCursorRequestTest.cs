using Client.Ui.Components.NodeGraph;
using Client.Ui.Foundation.Input.OsCursor;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UIElements;

namespace Client.Tests.Ui.Components.NodeGraph
{
    /// <summary>
    /// OS カーソルの要求の積み上げ（中断・外側の要求・カードの要素の層・非表示）で形が正しく戻ることを固定する
    /// Pins that the OS cursor shape returns correctly across aborts, outer requests, the card element layer and hiding
    /// </summary>
    public class NodeGraphViewCursorRequestTest : NodeGraphTestStage
    {
        [Test]
        public void 中断でパン中のクラスと握った手が戻り要求は掴む手だけが残る()
        {
            var view = MountView(MountHost(400f, 300f));
            var inside = ContentOrigin(view) + new Vector2(10f, 10f);
            simulate.MouseMove(new Vector2(5f, 5f));
            simulate.MouseMove(inside);
            simulate.MouseDown(inside, MouseButton.LeftMouse);
            simulate.MouseMove(inside + new Vector2(20f, 10f));
            Assert.IsTrue(view.ClassListContains("node-graph--panning"));
            Assert.AreEqual(UiOsCursorShape.Grabbing, AppliedCursor.Current);

            // 別の要素に捕捉を奪わせて中断を起こす
            // Let another element steal the capture to trigger the abort
            var thief = new VisualElement { name = "capture-thief" };
            rootVisualElement.Add(thief);
            thief.CapturePointer(PointerId.mousePointerId);
            simulate.MouseMove(inside + new Vector2(20f, 10f));
            simulate.FrameUpdate();
            thief.ReleasePointer(PointerId.mousePointerId);
            Assert.IsFalse(view.ClassListContains("node-graph--panning"), "class removed on abort");
            Assert.AreEqual(UiOsCursorShape.Grab, AppliedCursor.Current, "back to the grab hand");

            // 外へ出ると既定へ戻る（握った手の要求が残っていない）
            // Leaving returns to the default (no grabbing request is left)
            simulate.MouseMove(new Vector2(5f, 5f));
            simulate.FrameUpdate();
            Assert.AreEqual(UiOsCursorShape.Default, AppliedCursor.Current, "only the hover request remained");
        }

        [Test]
        public void 画面側の地の要求が積まれていてもビューポートを出ると画面の要求の形へ戻る()
        {
            var view = MountView(MountHost(400f, 300f));
            var inside = ContentOrigin(view) + new Vector2(10f, 10f);
            using (OsCursor.Request(UiOsCursorShape.Pointer, UiOsCursorLayer.Surface))
            {
                simulate.MouseMove(new Vector2(5f, 5f));
                simulate.MouseMove(inside);
                Assert.AreEqual(UiOsCursorShape.Grab, AppliedCursor.Current, "the viewport grab is the later surface request");
                simulate.MouseMove(new Vector2(5f, 5f));
                simulate.FrameUpdate();
                Assert.AreEqual(UiOsCursorShape.Pointer, AppliedCursor.Current, "back to the outer request");
            }
            Assert.AreEqual(UiOsCursorShape.Default, AppliedCursor.Current);
        }

        [Test]
        public void カード上で押して外へ出しカード上で離すとカードの指が地の掴む手に勝つ()
        {
            var view = MountView(MountHost(400f, 300f));
            var card = Card("card-a");
            new UiOsCursorHoverRequest(card, OsCursor, UiOsCursorShape.Pointer, UiOsCursorLayer.Element);
            view.SetGraph(new NodeGraphContent(new[] { Node("a", 0f, 0f, card) }, NodeGraphFocus.None));
            simulate.FrameUpdate();
            var onCard = card.worldBound.center;
            var outside = new Vector2(5f, 5f);

            // 地の掴む手 → カードの指 → パン中の握った手
            // Surface grab, then the card pointer, then the grabbing hand while panning
            simulate.MouseMove(outside);
            simulate.MouseMove(ContentOrigin(view) + new Vector2(10f, 10f));
            simulate.MouseMove(onCard);
            Assert.AreEqual(UiOsCursorShape.Pointer, AppliedCursor.Current, "card pointer over the surface grab");
            simulate.MouseDown(onCard, MouseButton.LeftMouse);
            simulate.MouseMove(onCard + new Vector2(20f, 10f));
            Assert.AreEqual(UiOsCursorShape.Grabbing, AppliedCursor.Current, "grabbing while panning");

            // 捕捉したまま外へ出て、押した点へ戻る（カードはパンで指に付いてくるのでカードの上）と離す
            // Leave while captured, return to the press point (the card follows the pan, so it is under the pointer), then release
            simulate.MouseMove(outside);
            Assert.AreEqual(UiOsCursorShape.Grabbing, AppliedCursor.Current, "still grabbing outside");
            simulate.MouseMove(onCard);
            Assert.IsTrue(card.worldBound.Contains(onCard), "the card followed the pan back under the pointer");
            simulate.MouseUp(onCard, MouseButton.LeftMouse);
            simulate.FrameUpdate();
            Assert.AreEqual(UiOsCursorShape.Pointer, AppliedCursor.Current, "the card pointer beats the re-taken surface grab");
        }

        [Test]
        public void 先に取った要素の層の指はビューポートが後から取った掴む手に勝つ()
        {
            // 要素の層の要求が先に生きていても、ビューポートの掴む手は地の層なので指が残る
            // An element-layer request taken first keeps the pointer, since the viewport grab is on the surface layer
            var view = MountView(MountHost(400f, 300f));
            using (OsCursor.Request(UiOsCursorShape.Pointer, UiOsCursorLayer.Element))
            {
                simulate.MouseMove(new Vector2(5f, 5f));
                simulate.MouseMove(ContentOrigin(view) + new Vector2(10f, 10f));
                simulate.FrameUpdate();
                CollectionAssert.AreEqual(new[] { UiOsCursorShape.Pointer }, AppliedCursor.AppliedShapes, "the later surface grab never overrides the element pointer");
            }
            Assert.AreEqual(UiOsCursorShape.Grab, AppliedCursor.Current, "the grab hand once the element pointer is withdrawn");
        }

        [Test]
        public void 大きさ0で非表示になると手の要求を全部放して既定へ戻る()
        {
            var host = MountHost(400f, 300f);
            var view = MountView(host);
            simulate.MouseMove(new Vector2(5f, 5f));
            simulate.MouseMove(ContentOrigin(view) + new Vector2(10f, 10f));
            Assert.AreEqual(UiOsCursorShape.Grab, AppliedCursor.Current);

            host.style.display = DisplayStyle.None;
            simulate.FrameUpdate();
            simulate.FrameUpdate();
            Assert.AreEqual(UiOsCursorShape.Default, AppliedCursor.Current);
        }

        [Test]
        public void カードの指に乗ったまま祖先を非表示にすると要求が外れて既定へ戻る()
        {
            // 画面を閉じる（祖先が display:none）と、乗ったままでも部品が指も掴む手も外す
            // Closing the screen (display:none ancestor) while hovered makes the part withdraw both the pointer and the grab
            var host = MountHost(400f, 300f);
            var view = MountView(host);
            var card = Card("card-a");
            new UiOsCursorHoverRequest(card, OsCursor, UiOsCursorShape.Pointer, UiOsCursorLayer.Element);
            view.SetGraph(new NodeGraphContent(new[] { Node("a", 0f, 0f, card) }, NodeGraphFocus.None));
            simulate.FrameUpdate();
            simulate.MouseMove(new Vector2(5f, 5f));
            simulate.MouseMove(card.worldBound.center);
            Assert.AreEqual(UiOsCursorShape.Pointer, AppliedCursor.Current, "the card pointer while hovered");

            host.style.display = DisplayStyle.None;
            simulate.FrameUpdate();
            simulate.FrameUpdate();
            Assert.AreEqual(UiOsCursorShape.Default, AppliedCursor.Current, "no leaked element request after hiding");

            // 外し損ねが無ければ、後から別の地の要求が当たる
            // With nothing leaked, a later surface request applies
            using (OsCursor.Request(UiOsCursorShape.Grab, UiOsCursorLayer.Surface))
            {
                Assert.AreEqual(UiOsCursorShape.Grab, AppliedCursor.Current, "a later surface request is not beaten by a leaked pointer");
            }
        }
    }
}
