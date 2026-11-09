using System.Linq;
using Client.Ui.Components.NodeGraph;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UIElements;

namespace Client.Tests.Ui.Components.NodeGraph
{
    /// <summary>
    /// ビューポートが動くだけでは中身を作り直さず、入力が変われば作り直す（Web TreeView.test.ts の写し）
    /// Moving the viewport alone never rebuilds the content; changing the input does (copy of the Web TreeView.test.ts)
    /// </summary>
    public class NodeGraphViewRenderTest : NodeGraphTestStage
    {
        /// <web-test file="src/shared/treeView/TreeView.test.ts">does not rebuild nodes when only the viewport moves</web-test>
        [Test]
        public void ビューポートが動くだけではノードを作り直さない()
        {
            var view = MountView(MountHost(400f, 300f));
            var card = Card("card-a");
            view.SetGraph(new NodeGraphContent(new[] { Node("a", 0f, 0f, card), Node("b", 100f, 0f, Card("card-b"), "a") }, NodeGraphFocus.None));
            simulate.FrameUpdate();
            var wrappersBefore = CanvasOf(view).Query(className: "node-graph__node").ToList();
            var edgesBefore = CanvasOf(view)[0];

            // ドラッグでパンし、ホイールでズームする
            // Pan by dragging, then zoom with the wheel
            Drag(ContentOrigin(view) + new Vector2(10f, 10f), new Vector2(20f, 10f));
            WheelNotches(view, new Vector2(50f, 50f), -1f);

            var viewport = ReadViewport(view);
            Assert.AreNotEqual(0f, viewport.X, "the viewport moved");
            CollectionAssert.AreEqual(wrappersBefore, CanvasOf(view).Query(className: "node-graph__node").ToList());
            Assert.AreSame(edgesBefore, CanvasOf(view)[0]);
            Assert.AreSame(wrappersBefore[0], card.parent);
        }

        /// <web-test file="src/shared/treeView/TreeView.test.ts">rebuilds the scene when render inputs change</web-test>
        [Test]
        public void 入力が変わると場面を作り直す()
        {
            var view = MountView(MountHost(400f, 300f));
            var firstCard = Card("card-first");
            view.SetGraph(new NodeGraphContent(new[] { Node("a", 0f, 0f, firstCard) }, NodeGraphFocus.None));
            simulate.FrameUpdate();
            var firstWrapper = firstCard.parent;

            var secondCard = Card("card-second");
            view.SetGraph(new NodeGraphContent(new[] { Node("a", 0f, 0f, secondCard), Node("b", 0f, -100f, Card("card-b"), "a") }, NodeGraphFocus.None));
            simulate.FrameUpdate();

            var wrappers = CanvasOf(view).Query(className: "node-graph__node").ToList();
            Assert.AreEqual(2, wrappers.Count);
            Assert.IsFalse(wrappers.Contains(firstWrapper), "the old wrapper is gone");
            Assert.IsNull(firstCard.panel, "the old card left the panel");
            Assert.AreSame(view.panel, secondCard.panel);
            Assert.AreEqual(400f, CanvasOf(view).resolvedStyle.width, 0.5f, "canvas width follows the new bounds");
            Assert.AreEqual(500f, CanvasOf(view).resolvedStyle.height, 0.5f, "canvas height follows the new bounds");
        }
    }
}
