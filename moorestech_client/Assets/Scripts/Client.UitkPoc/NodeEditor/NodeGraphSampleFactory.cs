using Client.UitkPoc.NodeEditor.Model;
using Client.UitkPoc.NodeEditor.View;
using UnityEngine;

namespace Client.UitkPoc.NodeEditor
{
    // 試験用のグラフを作る。列ごとに並べ、各ノードの出力を次の列のどこかの入力へつなぐ
    // Builds test graphs: nodes laid out in columns, each output wired to some input in the next column
    public static class NodeGraphSampleFactory
    {
        private static readonly string[] Titles = { "採掘機", "精錬炉", "加工機", "組立機", "ベルト", "分岐機", "チェスト", "発電機" };

        public static NodeGraphModel Create(int nodeCount)
        {
            var model = new NodeGraphModel();
            var random = new System.Random(11);
            var rows = Mathf.Max(3, Mathf.CeilToInt(Mathf.Sqrt(nodeCount * 0.6f)));
            var ids = new int[nodeCount];
            for (var i = 0; i < nodeCount; i++)
            {
                var column = i / rows;
                var row = i % rows;
                var inputs = column == 0 ? 0 : random.Next(1, 4);
                var outputs = random.Next(1, 3);
                var node = new EditorNode(model.AllocateId(), $"{Titles[random.Next(Titles.Length)]} {i + 1}", inputs, outputs, new Vector2(column * 280f, row * 140f), Vector2.zero);
                node.SetSize(new Vector2(200f, EditorNodeElement.MinHeight(node) + 8f));
                model.AddNode(node);
                ids[i] = node.Id;
            }

            // 次の列の近い行へつなぐ。同じ入力ポートへの重複は避ける
            // Wire to a nearby row in the next column, never twice into the same input port
            for (var i = 0; i + rows < nodeCount; i++)
            {
                var from = model.Nodes[ids[i]];
                for (var port = 0; port < from.OutputCount; port++)
                {
                    var targetIndex = Mathf.Clamp(i + rows + random.Next(-1, 2), (i / rows + 1) * rows, Mathf.Min(nodeCount - 1, (i / rows + 2) * rows - 1));
                    var to = model.Nodes[ids[targetIndex]];
                    if (to.InputCount == 0) continue;
                    var toPort = random.Next(to.InputCount);
                    if (model.FindEdgeIntoPort(to.Id, toPort) != null) continue;
                    model.AddEdge(new EditorEdge(model.AllocateId(), from.Id, port, to.Id, toPort));
                }
            }
            return model;
        }
    }
}
