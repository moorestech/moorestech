using Client.Ui.Components.NodeGraph;
using Client.Ui.Components.Panels;
using Client.Ui.Foundation.Input.OsCursor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Client.Ui.Editor.Gallery.Entries.NodeGraph
{
    /// <summary>
    /// NodeGraphView の見本。研究ツリーの参照（4-research.png）と同じ水平の線と傾き 148/364 の斜めの線を含む 4 ノード
    /// NodeGraphView sample; four nodes including a horizontal line and a 148/364 diagonal like the research reference (4-research.png)
    /// </summary>
    public sealed class NodeGraphViewGalleryEntry : IUiGalleryEntry
    {
        // 研究のノードカードの幅（Web ResearchTreePanel の .node width 132px）と見本の高さ
        // Research node card width (Web ResearchTreePanel .node width 132px) and the sample card height
        private const float CardWidth = 132f;
        private const float CardHeight = 56f;

        public UiGalleryFamily Family => UiGalleryFamily.NodeGraph;
        public string ComponentName => "NodeGraphView";

        public VisualElement CreateSample()
        {
            // 640×360 の枠いっぱいにビューポートを置き、根の右に水平・右下に斜めの子を並べる
            // Fill a 640×360 frame with the viewport; children sit to the right (horizontal) and lower right (diagonal) of the root
            var frame = new VisualElement();
            frame.style.width = 640;
            frame.style.height = 360;
            frame.style.flexDirection = FlexDirection.Column;
            // 面が窓の地と同色のため、研究画面の地（--mantine-color-dark-8 #1f1f1f）を枠に敷く
            // The card face matches the window backdrop, so lay the research screen backdrop (--mantine-color-dark-8 #1f1f1f) under the frame
            frame.style.backgroundColor = new Color(31f / 255f, 31f / 255f, 31f / 255f, 1f);
            var view = new NodeGraphView("gallery-node-graph", new UnityUiOsCursor());
            frame.Add(view);
            view.SetGraph(new NodeGraphContent(new[]
            {
                SampleNode("root", 0f, 0f),
                SampleNode("horizontal", 322f, 0f, "root"),
                SampleNode("diagonal", 364f, -148f, "root"),
                SampleNode("upper", -200f, 100f, "root"),
            }, NodeGraphFocus.At(new Vector2(100f, -40f))));
            return frame;
        }

        private static NodeGraphNode SampleNode(string key, float x, float y, params string[] previousKeys)
        {
            var card = new PaperSurface($"gallery-node-{key}");
            card.style.width = CardWidth;
            card.style.height = CardHeight;
            card.style.translate = new Translate(Length.Percent(-50f), Length.Percent(-50f));
            return new NodeGraphNode(key, new Vector2(x, y), previousKeys, card);
        }
    }
}
