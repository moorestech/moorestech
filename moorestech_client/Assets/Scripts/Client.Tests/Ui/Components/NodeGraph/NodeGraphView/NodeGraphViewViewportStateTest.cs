using Client.Ui.Components.NodeGraph;
using NUnit.Framework;
using UnityEngine;

namespace Client.Tests.Ui.Components.NodeGraph
{
    /// <summary>
    /// 視点の保持・中央寄せ・ホイール基準・寸法追従
    /// Viewport holding, centering, wheel anchor, resize-follow
    /// </summary>
    /// <remarks>
    /// Web の Map は View の出し入れで写す
    /// Web Map is played by hiding and re-showing the View
    /// </remarks>
    public class NodeGraphViewViewportStateTest : NodeGraphTestStage
    {
        /// <web-test file="src/shared/treeView/TreeViewViewportState.test.ts">restores the panned viewport after unmount and remount with the same key</web-test>
        [Test]
        public void 隠して戻してもパンしたビューポートのまま()
        {
            var host = MountHost(400f, 300f);
            var view = MountView(host);
            view.SetGraph(OneNode(NodeGraphFocus.None));
            simulate.FrameUpdate();
            Drag(ContentOrigin(view) + new Vector2(10f, 10f), new Vector2(10f, 5f));
            AssertViewport(10f, 5f, 1f, ReadViewport(view));

            HideAndShow(host, 300f);
            AssertViewport(10f, 5f, 1f, ReadViewport(view));
        }

        /// <web-test file="src/shared/treeView/TreeViewViewportState.test.ts">keeps the restored viewport across a size change</web-test>
        [Test]
        public void 隠している間に寸法が変わっても戻したとき中心を合わせ直さない()
        {
            var host = MountHost(400f, 300f);
            var view = MountView(host);
            view.SetGraph(OneNode(NodeGraphFocus.At(Vector2.zero)));
            simulate.FrameUpdate();
            AssertViewport(0f, -50f, 1f, ReadViewport(view));

            HideAndShow(host, 265f);
            AssertViewport(0f, -50f, 1f, ReadViewport(view));
        }

        /// <web-test file="src/shared/treeView/TreeViewViewportState.test.ts">centers the initial focus point when nothing is stored</web-test>
        [Test]
        public void 復元しなければ初回の注目点を中央に据える()
        {
            var view = MountView(MountHost(400f, 300f));
            view.SetGraph(OneNode(NodeGraphFocus.At(Vector2.zero)));
            simulate.FrameUpdate();
            AssertViewport(0f, -50f, 1f, ReadViewport(view));
        }

        /// <web-test file="src/shared/treeView/TreeViewViewportState.test.ts">centers when the focus point arrives after the first data push</web-test>
        [Test]
        public void 注目点が後から届いたときに中央へ寄せる()
        {
            var view = MountView(MountHost(400f, 300f));
            view.SetGraph(OneNode(NodeGraphFocus.None));
            simulate.FrameUpdate();
            AssertViewport(0f, 0f, 1f, ReadViewport(view));

            view.SetGraph(OneNode(NodeGraphFocus.At(Vector2.zero)));
            simulate.FrameUpdate();
            AssertViewport(0f, -50f, 1f, ReadViewport(view));
        }

        /// <web-test file="src/shared/treeView/TreeViewViewportState.test.ts">prefers the stored viewport over initial-focus centering</web-test>
        [Test]
        public void 保持したビューポートを初回の中央寄せより優先する()
        {
            var host = MountHost(400f, 300f);
            var view = MountView(host);
            Drag(ContentOrigin(view) + new Vector2(10f, 10f), new Vector2(30f, 40f));
            HideAndShow(host, 300f);

            view.SetGraph(OneNode(NodeGraphFocus.At(Vector2.zero)));
            simulate.FrameUpdate();
            AssertViewport(30f, 40f, 1f, ReadViewport(view));
        }

        /// <web-test file="src/shared/treeView/TreeViewViewportState.test.ts">keeps the pre-data zoom scale when centering fires after nodes arrive</web-test>
        [Test]
        public void ノードの到着前のズームの倍率を中央寄せで保つ()
        {
            var view = MountView(MountHost(400f, 300f));
            WheelNotches(view, new Vector2(200f, 150f), -1f);
            var scale = ReadViewport(view).Scale;
            Assert.Greater(scale, 1f, "zoomed in before data");

            view.SetGraph(OneNode(NodeGraphFocus.At(Vector2.zero)));
            simulate.FrameUpdate();
            AssertViewport(200f - 200f * scale, 150f - 200f * scale, scale, ReadViewport(view));
        }

        /// <web-test file="src/shared/treeView/TreeViewViewportState.test.ts">anchors wheel zoom at the content box, so the clearance does not shift the view</web-test>
        [Test]
        public void ホイールのズームは内容boxを基準にし逃げで視野がずれない()
        {
            var view = MountView(MountHost(400f, 300f));
            WheelNotches(view, Vector2.zero, -1f);
            var viewport = ReadViewport(view);
            Assert.AreEqual(0f, viewport.X, Tolerance);
            Assert.AreEqual(0f, viewport.Y, Tolerance);
            Assert.AreEqual(Mathf.Exp(60f * 0.0015f), viewport.Scale, 1e-3f, "one notch equals the Web deltaY 60");
        }

        [Test]
        public void 寸法が縮むと中央に見えていた点を中央に保つ()
        {
            var host = MountHost(400f, 300f);
            var view = MountView(host);
            view.SetGraph(OneNode(NodeGraphFocus.At(Vector2.zero)));
            simulate.FrameUpdate();
            host.style.height = 265f;
            simulate.FrameUpdate();
            simulate.FrameUpdate();
            AssertViewport(0f, -67.5f, 1f, ReadViewport(view));
        }

        private static NodeGraphContent OneNode(NodeGraphFocus focus)
        {
            return new NodeGraphContent(new[] { Node("a", 0f, 0f, Card("card-a")) }, focus);
        }
    }
}
