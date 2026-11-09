using System.Collections.Generic;
using Client.Ui.Components.NodeGraph;
using NUnit.Framework;
using UniRx;
using UnityEngine;

namespace Client.Tests.Ui.Components.NodeGraph
{
    /// <summary>
    /// ビューポートの復元・初回の中央寄せ・ホイールの基準・寸法の追従（Web TreeViewViewportState.test.ts の 1〜6 件目の写し）
    /// Viewport restore, initial centering, wheel anchoring and resize-follow (copy of tests 1–6 of the Web TreeViewViewportState.test.ts)
    /// </summary>
    /// <remarks>
    /// Web の viewportKey ごとの Map は画面の State が持つ。ここでは画面の役を「OnViewportChanged の最後の値を控え、作り直しで RestoreViewport へ渡す」で写す
    /// The Web per-viewportKey Map is held by the screen State; here the screen's role is "keep the last OnViewportChanged value and pass it to RestoreViewport on rebuild"
    /// </remarks>
    public class NodeGraphViewViewportStateTest : NodeGraphTestStage
    {
        /// <web-test file="src/shared/treeView/TreeViewViewportState.test.ts">restores the panned viewport after unmount and remount with the same key</web-test>
        [Test]
        public void 流れた最後の値で作り直すとパンしたビューポートを復元する()
        {
            var host = MountHost(400f, 300f);
            var first = MountView(host);
            var held = new List<NodeGraphViewport>();
            first.OnViewportChanged.Subscribe(held.Add);
            first.SetGraph(OneNode(NodeGraphFocus.None));
            simulate.FrameUpdate();
            Drag(ContentOrigin(first) + new Vector2(10f, 10f), new Vector2(10f, 5f));
            first.RemoveFromHierarchy();
            Assert.IsNotEmpty(held, "panning emits the viewport");

            // 画面の役: 最後の値を新しいビューへ戻す。戻した値は流れない
            // Screen role: restore the last value into the new view; the restored value is not emitted
            var emittedAfterRestore = new List<NodeGraphViewport>();
            var second = MountRestoredView(host, held[held.Count - 1], emittedAfterRestore);
            second.SetGraph(OneNode(NodeGraphFocus.None));
            simulate.FrameUpdate();
            AssertViewport(10f, 5f, 1f, ReadViewport(second));
            Assert.IsEmpty(emittedAfterRestore, "restore and mount emit nothing");
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
        public void 戻したビューポートを初回の中央寄せより優先する()
        {
            var view = MountRestoredView(MountHost(400f, 300f), new NodeGraphViewport(30f, 40f, 1f), new List<NodeGraphViewport>());
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
