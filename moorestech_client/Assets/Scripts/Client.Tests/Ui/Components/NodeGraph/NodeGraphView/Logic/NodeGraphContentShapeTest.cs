using Client.Ui.Components.NodeGraph;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UIElements;

namespace Client.Tests.Ui.Components.NodeGraph
{
    /// <summary>
    /// グラフの形の比較（NodeGraphView.SetGraph が組み直しを省く判定。研究とチャレンジが共有する）（L0）
    /// Graph-shape comparison (the check NodeGraphView.SetGraph uses to skip a rebuild; shared by research and challenges) (L0)
    /// </summary>
    public class NodeGraphContentShapeTest
    {
        private readonly VisualElement _cardA = new();
        private readonly VisualElement _cardB = new();

        [Test]
        public void 並びとキーと座標と前提とカードと注目点が同じなら同じ形()
        {
            // 毎回組み直した別インスタンスでも中身が同じなら同じ形
            // Separately built instances with the same contents share a shape
            var shown = Content(NodeGraphFocus.At(Vector2.zero), NodeA(), NodeB(new Vector2(220f, 0f), "a"));
            Assert.IsTrue(shown.HasSameShapeAs(Content(NodeGraphFocus.At(Vector2.zero), NodeA(), NodeB(new Vector2(220f, 0f), "a"))));
            Assert.IsTrue(Content(NodeGraphFocus.None, NodeA()).HasSameShapeAs(Content(NodeGraphFocus.None, NodeA())), "注目点なし同士");
        }

        [Test]
        public void 座標か前提か並びかカードか注目点が変われば違う形()
        {
            var shown = Content(NodeGraphFocus.At(Vector2.zero), NodeA(), NodeB(new Vector2(220f, 0f), "a"));

            // 形の要素のどれか 1 つの違いで組み直しが要る
            // A difference in any one shape element needs a rebuild
            Assert.IsFalse(shown.HasSameShapeAs(Content(NodeGraphFocus.At(Vector2.zero), NodeA(), NodeB(new Vector2(240f, 0f), "a"))), "座標");
            Assert.IsFalse(shown.HasSameShapeAs(Content(NodeGraphFocus.At(Vector2.zero), NodeA(), NodeB(new Vector2(220f, 0f)))), "前提");
            Assert.IsFalse(shown.HasSameShapeAs(Content(NodeGraphFocus.At(Vector2.zero), NodeB(new Vector2(220f, 0f), "a"), NodeA())), "並び");
            Assert.IsFalse(shown.HasSameShapeAs(Content(NodeGraphFocus.At(Vector2.zero), NodeA())), "数");
            Assert.IsFalse(shown.HasSameShapeAs(Content(NodeGraphFocus.At(Vector2.zero), new NodeGraphNode("a", Vector2.zero, new string[0], new VisualElement()), NodeB(new Vector2(220f, 0f), "a"))), "カード");
            Assert.IsFalse(shown.HasSameShapeAs(Content(NodeGraphFocus.At(new Vector2(220f, 0f)), NodeA(), NodeB(new Vector2(220f, 0f), "a"))), "注目点");
            Assert.IsFalse(shown.HasSameShapeAs(Content(NodeGraphFocus.None, NodeA(), NodeB(new Vector2(220f, 0f), "a"))), "注目点の有無");
        }

        private static NodeGraphContent Content(NodeGraphFocus focus, params NodeGraphNode[] nodes)
        {
            return new NodeGraphContent(nodes, focus);
        }

        private NodeGraphNode NodeA()
        {
            return new NodeGraphNode("a", Vector2.zero, new string[0], _cardA);
        }

        private NodeGraphNode NodeB(Vector2 position, params string[] previousKeys)
        {
            return new NodeGraphNode("b", position, previousKeys, _cardB);
        }
    }
}
