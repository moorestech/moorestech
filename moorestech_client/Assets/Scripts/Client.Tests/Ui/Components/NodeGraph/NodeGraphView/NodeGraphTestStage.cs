using System.Collections.Generic;
using Client.Tests.Ui.TestHost;
using Client.Ui.Components.NodeGraph;
using NUnit.Framework;
using UniRx;
using UnityEngine;
using UnityEngine.UIElements;

namespace Client.Tests.Ui.Components.NodeGraph
{
    /// <summary>
    /// NodeGraphView の L1 の土台。親 400×300 に置き、逃げ 12px で内容 box が 400×300 になる（Web テストの CLIP_INSET 12・内容 400×300）
    /// L1 base for NodeGraphView; mounted in a 400×300 parent so the 12 px clearance yields a 400×300 content box (Web test CLIP_INSET 12, content 400×300)
    /// </summary>
    public abstract class NodeGraphTestStage : UiTestHost
    {
        protected const float Tolerance = 0.01f;
        protected const float CardWidth = 60f;
        protected const float CardHeight = 40f;

        protected FakeUiOsCursor OsCursor { get; private set; }

        [SetUp]
        public void ZeroSimulatedFrameTime()
        {
            // 事象ごとの自動加算（既定 200ms）を 0 にし、ポインタの時刻を FrameUpdateMs だけで進める
            // Zero the per-event auto increment (default 200 ms) so pointer timestamps advance only by FrameUpdateMs
            simulate.timePerSimulatedFrameMs = 0;
            OsCursor = new FakeUiOsCursor();
        }

        [TearDown]
        public void ResetSimulatedFrameTime()
        {
            // 固定値はフィクスチャで自動復帰しないので戻す
            // The fixed value is not reset by the fixture, so restore it
            simulate.ResetTimePerSimulatedFrameToDefault();
        }

        protected VisualElement MountHost(float width, float height)
        {
            var host = new VisualElement { name = "node-graph-host" };
            host.style.position = Position.Absolute;
            host.style.left = 40;
            host.style.top = 40;
            host.style.width = width;
            host.style.height = height;
            rootVisualElement.Add(host);
            return host;
        }

        protected NodeGraphView MountView(VisualElement host)
        {
            return Settle(host, new NodeGraphView("test-graph", OsCursor));
        }

        protected NodeGraphView MountRestoredView(VisualElement host, NodeGraphViewport stored, List<NodeGraphViewport> emitted)
        {
            // 画面の作り直しと同じく、作った直後に購読し、載せる前に保持した値へ戻す
            // As a screen rebuilds: subscribe right after creation, then restore the held value before mounting
            var view = new NodeGraphView("test-graph", OsCursor);
            view.OnViewportChanged.Subscribe(emitted.Add);
            view.RestoreViewport(stored);
            return Settle(host, view);
        }

        private NodeGraphView Settle(VisualElement host, NodeGraphView view)
        {
            // 逃げの読み取り → padding と margin → レイアウトの 2 巡で落ち着く
            // Settles in two passes: read the clearance, then lay out with padding and margin
            host.Add(view);
            simulate.FrameUpdate();
            simulate.FrameUpdate();
            return view;
        }

        protected static VisualElement Card(string cardName)
        {
            var card = new VisualElement { name = cardName };
            card.style.width = CardWidth;
            card.style.height = CardHeight;
            card.style.translate = new Translate(Length.Percent(-50f), Length.Percent(-50f));
            return card;
        }

        protected static NodeGraphNode Node(string key, float x, float y, VisualElement card, params string[] previousKeys)
        {
            return new NodeGraphNode(key, new Vector2(x, y), previousKeys, card);
        }

        protected static VisualElement CanvasOf(NodeGraphView view)
        {
            return view.Q("test-graph-canvas");
        }

        protected static NodeGraphViewport ReadViewport(NodeGraphView view)
        {
            var canvas = CanvasOf(view);
            var translate = canvas.style.translate.value;
            return new NodeGraphViewport(translate.x.value, translate.y.value, canvas.style.scale.value.value.x);
        }

        protected static Vector2 ContentOrigin(NodeGraphView view)
        {
            return view.LocalToWorld(view.contentRect.position);
        }

        protected void WheelNotches(NodeGraphView view, Vector2 contentPoint, float notches)
        {
            // 正の delta.y が下（Web の deltaY > 0 ＝縮小）。1 ノッチは scrollDeltaPerTick
            // Positive delta.y is down (Web deltaY > 0 = zoom out); one notch is scrollDeltaPerTick
            simulate.ScrollWheel(new Vector2(0f, notches * WheelEvent.scrollDeltaPerTick), ContentOrigin(view) + contentPoint);
            simulate.FrameUpdate();
        }

        protected void Drag(Vector2 from, Vector2 delta)
        {
            simulate.MouseDown(from, MouseButton.LeftMouse);
            simulate.MouseMove(from + delta);
            simulate.MouseUp(from + delta, MouseButton.LeftMouse);
            simulate.FrameUpdate();
        }

        protected static void AssertViewport(float x, float y, float scale, NodeGraphViewport actual)
        {
            Assert.AreEqual(x, actual.X, Tolerance, "x");
            Assert.AreEqual(y, actual.Y, Tolerance, "y");
            Assert.AreEqual(scale, actual.Scale, Tolerance, "scale");
        }
    }
}
