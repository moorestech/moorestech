using Client.UitkPoc.NodeEditor.Model;
using UnityEngine;
using UnityEngine.UIElements;

namespace Client.UitkPoc.NodeEditor.View
{
    // ポートの所在。ポート要素のuserDataに入れ、入力処理がPickした要素から引く
    // Where a port lives; stored in the port element's userData and read back after Pick
    public class PortRef
    {
        public readonly int NodeId;
        public readonly int Index;
        public readonly bool IsInput;

        public PortRef(int nodeId, int index, bool isInput)
        {
            NodeId = nodeId;
            Index = index;
            IsInput = isInput;
        }
    }

    // ノード1枚。見出し・左右のポート・右下の大きさ変更の取っ手を持つ
    // One node card: header, ports on both sides and a resize grip at the bottom-right
    public class EditorNodeElement : VisualElement
    {
        public const float HeaderHeight = 28f;
        public const float FirstPortCenter = 44f;
        public const float PortSpacing = 24f;
        public const float MinWidth = 140f;

        public readonly int NodeId;

        public EditorNodeElement(EditorNode node)
        {
            NodeId = node.Id;
            AddToClassList("ne-node");
            var header = new Label(node.Title);
            header.AddToClassList("ne-node__header");
            header.pickingMode = PickingMode.Ignore;
            Add(header);
            for (var i = 0; i < node.InputCount; i++) AddPort(i, true);
            for (var i = 0; i < node.OutputCount; i++) AddPort(i, false);
            var grip = new VisualElement();
            grip.AddToClassList("ne-node__grip");
            Add(grip);
            Sync(node);
        }

        // 位置はtranslateで持つ。left/topだと動かすたびにレイアウトが走る
        // Position lives in translate; left/top would trigger a layout pass on every move
        public void Sync(EditorNode node)
        {
            style.translate = new Translate(node.Position.x, node.Position.y);
            style.width = node.Size.x;
            style.height = node.Size.y;
        }

        public void SetSelected(bool selected)
        {
            EnableInClassList("ne-node--selected", selected);
        }

        public static Vector2 PortPosition(EditorNode node, bool isInput, int index)
        {
            return node.Position + new Vector2(isInput ? 0f : node.Size.x, FirstPortCenter + index * PortSpacing);
        }

        public static float MinHeight(EditorNode node)
        {
            return FirstPortCenter + Mathf.Max(node.InputCount, node.OutputCount) * PortSpacing;
        }

        private void AddPort(int index, bool isInput)
        {
            // ポートの丸は縁にまたがって置き、ラベルは内側に添える
            // The port dot straddles the border and its label sits inside
            var top = FirstPortCenter + index * PortSpacing;
            var port = new VisualElement { userData = new PortRef(NodeId, index, isInput) };
            port.AddToClassList("ne-port");
            port.AddToClassList(isInput ? "ne-port--in" : "ne-port--out");
            port.style.top = top - 7f;
            Add(port);
            var label = new Label(isInput ? $"入力{index + 1}" : $"出力{index + 1}");
            label.AddToClassList("ne-port-label");
            label.AddToClassList(isInput ? "ne-port-label--in" : "ne-port-label--out");
            label.pickingMode = PickingMode.Ignore;
            label.style.top = top - 9f;
            Add(label);
        }
    }
}
