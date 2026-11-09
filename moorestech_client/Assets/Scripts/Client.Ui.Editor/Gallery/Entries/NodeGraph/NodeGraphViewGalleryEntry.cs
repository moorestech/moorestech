using Client.Ui.Components.NodeGraph;
using Client.Ui.Components.Panels;
using Client.Ui.Foundation.Input.OsCursor;
using UnityEditor;
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
        private const string SheetPath = "Assets/Scripts/Client.Ui.Editor/Gallery/Entries/NodeGraph/NodeGraphGallerySamples.uss";

        public UiGalleryFamily Family => UiGalleryFamily.NodeGraph;
        public string ComponentName => "NodeGraphView";

        public VisualElement CreateSample()
        {
            // 640×360 の枠いっぱいにビューポートを置き、根の右に水平・右下に斜めの子を並べる
            // Fill a 640×360 frame with the viewport; children sit to the right (horizontal) and lower right (diagonal) of the root
            var frame = new VisualElement();
            frame.AddToClassList("gallery-node-graph");
            // 寸法と地が欠けると参照と比べられないので、開発者へ知らせて素の枠で続ける
            // Without the sizes and backdrop nothing can be compared, so tell the developer and continue unstyled
            var sheet = AssetDatabase.LoadAssetAtPath<StyleSheet>(SheetPath);
            if (sheet == null) Debug.LogError($"[UiGallery] sample sheet not found: {SheetPath}");
            else frame.styleSheets.Add(sheet);
            var view = new NodeGraphView("gallery-node-graph", new ProcessUiOsCursor());
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
            card.AddToClassList("gallery-node-graph__card");
            return new NodeGraphNode(key, new Vector2(x, y), previousKeys, card);
        }
    }
}
