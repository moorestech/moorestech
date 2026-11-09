using Client.Ui.Components.NodeGraph;
using Client.Ui.Foundation.Input.OsCursor;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UIElements;

namespace Client.Tests.Ui.Components.NodeGraph
{
    /// <summary>
    /// OS カーソルの要求の積み上げ（中断・外側の要求・非表示）で形が正しく戻ることを固定する
    /// Pins that the OS cursor shape returns correctly across aborts, outer requests and hiding
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
            Assert.AreEqual(UiOsCursorShape.Grabbing, OsCursor.Current);

            // 別の要素に捕捉を奪わせて中断を起こす
            // Let another element steal the capture to trigger the abort
            var thief = new VisualElement { name = "capture-thief" };
            rootVisualElement.Add(thief);
            thief.CapturePointer(PointerId.mousePointerId);
            simulate.MouseMove(inside + new Vector2(20f, 10f));
            simulate.FrameUpdate();
            thief.ReleasePointer(PointerId.mousePointerId);
            Assert.IsFalse(view.ClassListContains("node-graph--panning"), "class removed on abort");
            Assert.AreEqual(UiOsCursorShape.Grab, OsCursor.Current, "back to the grab hand");
            Assert.AreEqual(1, OsCursor.LiveRequestCount, "only the hover request remains");
        }

        [Test]
        public void 画面側の要求が積まれていてもビューポートを出ると画面の要求の形へ戻る()
        {
            var view = MountView(MountHost(400f, 300f));
            var inside = ContentOrigin(view) + new Vector2(10f, 10f);
            using (OsCursor.Request(UiOsCursorShape.Grab))
            {
                simulate.MouseMove(new Vector2(5f, 5f));
                simulate.MouseMove(inside);
                simulate.MouseMove(new Vector2(5f, 5f));
                simulate.FrameUpdate();
                Assert.AreEqual(UiOsCursorShape.Grab, OsCursor.Current, "the outer request is still live");
                Assert.AreEqual(1, OsCursor.LiveRequestCount);
            }
        }

        [Test]
        public void 大きさ0で非表示になると手の要求を全部放して既定へ戻る()
        {
            var host = MountHost(400f, 300f);
            var view = MountView(host);
            simulate.MouseMove(new Vector2(5f, 5f));
            simulate.MouseMove(ContentOrigin(view) + new Vector2(10f, 10f));
            Assert.AreEqual(UiOsCursorShape.Grab, OsCursor.Current);

            host.style.display = DisplayStyle.None;
            simulate.FrameUpdate();
            simulate.FrameUpdate();
            Assert.AreEqual(UiOsCursorShape.Default, OsCursor.Current);
            Assert.AreEqual(0, OsCursor.LiveRequestCount);
        }
    }
}
