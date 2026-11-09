using System.Text.RegularExpressions;
using Client.Tests.Ui.Components;
using Client.Ui.Components.NodeGraph;
using Client.Ui.Foundation.Anchor;
using Client.Ui.Foundation.Input.OsCursor;
using Client.Ui.Foundation.Theme;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace Client.Tests.Ui.Components.NodeGraph
{
    /// <summary>
    /// NodeGraphView の寸法・線の値・つまみ・重なり順・変換の原点・入力の誤りの知らせ
    /// NodeGraphView sizing, line values, knobs, stacking, transform origin and input-error reporting
    /// </summary>
    public class NodeGraphViewLookTest : NodeGraphTestStage
    {
        private const string ProbePath = "Assets/Scripts/Client.Tests/Ui/Components/NodeGraph/NodeGraphView/NodeGraphKnobOverrides.uss";

        [Test]
        public void 内容boxは親と同じ寸法でクリップの逃げはpaddingと負のmarginが同量()
        {
            var view = MountView(MountHost(400f, 300f));
            var inset = ThemeTokenFile.ResolvePixels("--tutorial-anchor-clip-inset");
            Assert.AreEqual(400f, view.contentRect.width, 0.5f);
            Assert.AreEqual(300f, view.contentRect.height, 0.5f);
            Assert.AreEqual(inset, view.resolvedStyle.paddingLeft, 0.5f);
            Assert.AreEqual(-inset, view.resolvedStyle.marginTop, 0.5f);
        }

        [Test]
        public void ビューポートはチュートリアルの切る容器として印が付く()
        {
            var view = MountView(MountHost(400f, 300f));
            Assert.IsTrue(view.ClassListContains(TutorialAnchor.ClipContainerClassName));
        }

        [Test]
        public void 祖先でクリップの逃げを上書きするとpaddingと負のmarginが同じ量で追従する()
        {
            var probe = AssetDatabase.LoadAssetAtPath<StyleSheet>(ProbePath);
            Assert.IsNotNull(probe, ProbePath);
            rootVisualElement.styleSheets.Add(probe);
            var host = MountHost(400f, 300f);
            host.AddToClassList("node-graph-knob-probe");
            var view = MountView(host);
            Assert.AreEqual(20f, view.resolvedStyle.paddingRight, 0.5f);
            Assert.AreEqual(-20f, view.resolvedStyle.marginLeft, 0.5f);
            Assert.AreEqual(400f, view.contentRect.width, 0.5f);
        }

        [Test]
        public void 線は高さ3pxで色はmantineのdark4で中心は点から1_5px下()
        {
            var view = MountView(MountHost(400f, 300f));
            var edges = view.Q(className: "node-graph__edges");
            Assert.IsTrue(UssCustomStyleReader.TryReadColor(edges.customStyle, "--node-graph-edge-color", out var color));
            Assert.IsTrue(UssCustomStyleReader.TryReadPixels(edges.customStyle, "--node-graph-edge-width", out var width));
            ThemeTokenFile.AssertSameColor(ThemeTokenFile.ResolveColor("--mantine-color-dark-4"), color, "edge color");
            Assert.AreEqual(3f, width, Tolerance);
            var start = NodeGraphEdgeLayer.StrokeStart(NodeGraphGeometry.LineBetween(new Vector2(10f, 20f), new Vector2(50f, 20f)), width);
            Assert.AreEqual(new Vector2(10f, 21.5f), start);
        }

        [Test]
        public void 線の層はノードより下で拾わずカードは寸法を保ちキャンバスは左上を原点に拡縮する()
        {
            var view = MountView(MountHost(400f, 300f));
            var card = Card("card-a");
            view.SetGraph(new NodeGraphContent(new[] { Node("a", 0f, 0f, card) }, NodeGraphFocus.None));
            simulate.FrameUpdate();
            var canvas = CanvasOf(view);
            Assert.IsTrue(canvas[0].ClassListContains("node-graph__edges"), "edge layer is the first child");
            Assert.AreEqual(PickingMode.Ignore, canvas[0].pickingMode);

            // 高さ 0 の包みの中でもカードは自分の寸法を保つ（flex-shrink の初期値 1 で潰れない）
            // The card keeps its own size inside the zero-height wrapper (not squashed by the default flex-shrink 1)
            Assert.AreEqual(CardWidth, card.worldBound.width, 0.5f);
            Assert.AreEqual(CardHeight, card.worldBound.height, 0.5f);

            // 内容 box の原点で 1 ノッチ拡大すると、キャンバス点 (200,200) は原点から 200×倍率の位置に来る
            // One notch of zoom at the content origin puts canvas point (200,200) at 200×scale from the origin
            WheelNotches(view, Vector2.zero, -1f);
            var scale = ReadViewport(view).Scale;
            var expected = ContentOrigin(view) + new Vector2(200f, 200f) * scale;
            Assert.AreEqual(expected.x, card.worldBound.center.x, 0.5f);
            Assert.AreEqual(expected.y, card.worldBound.center.y, 0.5f);
        }

        [Test]
        public void ドラッグ中だけnode_graph__panningが付き離すと外れる()
        {
            var view = MountView(MountHost(400f, 300f));
            var from = ContentOrigin(view) + new Vector2(10f, 10f);

            // 閾値を越えて動かした間だけ握った手のクラスが付く
            // The grabbing class is present only while moved past the threshold
            simulate.MouseDown(from, MouseButton.LeftMouse);
            Assert.IsFalse(view.ClassListContains("node-graph--panning"), "not before the threshold");
            simulate.MouseMove(from + new Vector2(20f, 10f));
            Assert.IsTrue(view.ClassListContains("node-graph--panning"), "while dragging");

            // 離すと外れる
            // Releasing removes it
            simulate.MouseUp(from + new Vector2(20f, 10f), MouseButton.LeftMouse);
            simulate.FrameUpdate();
            Assert.IsFalse(view.ClassListContains("node-graph--panning"), "after release");
        }

        [Test]
        public void ビューポートに入ると掴む手_パン中は握った手_離すと掴む手_出ると既定()
        {
            var view = MountView(MountHost(400f, 300f));
            var outside = new Vector2(5f, 5f);
            var inside = ContentOrigin(view) + new Vector2(10f, 10f);

            // 外から入ると掴む手、閾値を越えたドラッグで握った手
            // Entering from outside shows grab; a drag past the threshold shows grabbing
            simulate.MouseMove(outside);
            simulate.MouseMove(inside);
            simulate.MouseDown(inside, MouseButton.LeftMouse);
            simulate.MouseMove(inside + new Vector2(20f, 10f));
            CollectionAssert.AreEqual(new[] { UiOsCursorShape.Grab, UiOsCursorShape.Grabbing }, OsCursor.AppliedShapes);

            // 中で離すと掴む手へ戻り、外へ出ると既定へ戻る
            // Releasing inside returns to grab; leaving returns to the default
            simulate.MouseUp(inside + new Vector2(20f, 10f), MouseButton.LeftMouse);
            simulate.FrameUpdate();
            simulate.MouseMove(outside);
            simulate.FrameUpdate();
            CollectionAssert.AreEqual(new[] { UiOsCursorShape.Grab, UiOsCursorShape.Grabbing, UiOsCursorShape.Grab, UiOsCursorShape.Default }, OsCursor.AppliedShapes);
        }

        [Test]
        public void パン中に外へ出ても握った手のまま_外で離すと既定()
        {
            var view = MountView(MountHost(400f, 300f));
            var outside = new Vector2(5f, 5f);
            var inside = ContentOrigin(view) + new Vector2(10f, 10f);

            // 捕捉中に外へ出ても握った手を保つ
            // Keep grabbing while captured even outside
            simulate.MouseMove(outside);
            simulate.MouseMove(inside);
            simulate.MouseDown(inside, MouseButton.LeftMouse);
            simulate.MouseMove(inside + new Vector2(20f, 10f));
            simulate.MouseMove(outside);
            CollectionAssert.AreEqual(new[] { UiOsCursorShape.Grab, UiOsCursorShape.Grabbing }, OsCursor.AppliedShapes);

            // 外で離すと既定へ戻る
            // Releasing outside returns to the default
            simulate.MouseUp(outside, MouseButton.LeftMouse);
            simulate.FrameUpdate();
            CollectionAssert.AreEqual(new[] { UiOsCursorShape.Grab, UiOsCursorShape.Grabbing, UiOsCursorShape.Default }, OsCursor.AppliedShapes);
        }

        [Test]
        public void グラフに無い親のキーは線を描かずLogErrorで知らせる()
        {
            var view = MountView(MountHost(400f, 300f));
            LogAssert.Expect(LogType.Error, new Regex(@"\[NodeGraphView\] node 'b' refers to missing previous key 'ghost'"));
            view.SetGraph(new NodeGraphContent(new[] { Node("a", 0f, 0f, Card("card-a")), Node("b", 100f, 0f, Card("card-b"), "a", "ghost") }, NodeGraphFocus.None));
            simulate.FrameUpdate();
            Assert.AreEqual(2, CanvasOf(view).Query(className: "node-graph__node").ToList().Count);

            // 欠けた親は飛ばし、正しい親 a への 1 本だけが残る
            // The missing parent is skipped; only the line to a remains
            var lines = ((NodeGraphEdgeLayer)CanvasOf(view)[0]).Lines;
            Assert.AreEqual(1, lines.Count);
            Assert.AreEqual(300f, lines[0].Origin.x, 0.01f);
            Assert.AreEqual(200f, lines[0].Origin.y, 0.01f);
            Assert.AreEqual(100f, lines[0].Length, 0.01f);
        }

        [Test]
        public void 重なったキーは後のノードを置かずLogErrorで知らせる()
        {
            var view = MountView(MountHost(400f, 300f));
            var later = Card("card-later");
            LogAssert.Expect(LogType.Error, new Regex(@"\[NodeGraphView\] duplicate node key 'a'"));
            view.SetGraph(new NodeGraphContent(new[] { Node("a", 0f, 0f, Card("card-first")), Node("a", 50f, 0f, later) }, NodeGraphFocus.None));
            simulate.FrameUpdate();
            Assert.AreEqual(1, CanvasOf(view).Query(className: "node-graph__node").ToList().Count);
            Assert.IsNull(later.parent, "the later card is not placed");
        }
    }
}
