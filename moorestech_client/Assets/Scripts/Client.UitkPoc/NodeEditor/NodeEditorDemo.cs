using System.Collections.Generic;
using System.Linq;
using Client.UitkPoc.NodeEditor.Model;
using Client.UitkPoc.NodeEditor.View;
using Client.UitkPoc.Shell;
using UnityEngine;
using UnityEngine.UIElements;

namespace Client.UitkPoc.NodeEditor
{
    // React Flow相当の検証。ポート接続・付け替え・範囲選択・吸着・ミニマップ・元に戻す・大きさ変更と、ノード数での負荷
    // React Flow-like checks: port wiring, reconnect, marquee, snapping, minimap, undo, resize and load by node count
    public class NodeEditorDemo : IUitkPocDemo
    {
        private static readonly int[] NodeCounts = { 12, 200, 1000, 2000 };

        private VisualElement _canvasHost;
        private Label _status;

        public string Title => "ノード編集";
        public NodeEditorCanvas Canvas { get; private set; }

        public NodeEditorDemo(UitkPocContext context)
        {
        }

        public VisualElement CreateView()
        {
            var root = new VisualElement();
            root.styleSheets.Add(Resources.Load<StyleSheet>("UitkPoc/NodeEditor"));
            root.AddToClassList("ne-root");
            var toolbar = new VisualElement();
            toolbar.AddToClassList("ne-toolbar");
            var count = new DropdownField("ノード数", NodeCounts.Select(c => c.ToString()).ToList(), 0);
            count.RegisterValueChangedCallback(_ => Rebuild(NodeCounts[count.index]));
            var snap = new Toggle("方眼に吸着") { value = true };
            snap.RegisterValueChangedCallback(evt => Canvas.SetSnapToGrid(evt.newValue));
            toolbar.Add(count);
            toolbar.Add(snap);
            toolbar.Add(new Button(() => Canvas.History.Undo()) { text = "元に戻す" });
            toolbar.Add(new Button(() => Canvas.History.Redo()) { text = "やり直す" });
            toolbar.Add(new Button(() => Canvas.FitToView()) { text = "全体表示" });
            _status = new Label();
            _status.AddToClassList("ne-status");
            toolbar.Add(_status);
            root.Add(toolbar);
            _canvasHost = new VisualElement();
            _canvasHost.AddToClassList("ne-canvas-host");
            root.Add(_canvasHost);
            root.Add(Hint());
            Rebuild(NodeCounts[0]);
            root.schedule.Execute(UpdateStatus).Every(200);
            return root;
        }

        public void Rebuild(int nodeCount)
        {
            _canvasHost.Clear();
            Canvas = new NodeEditorCanvas(NodeGraphSampleFactory.Create(nodeCount));
            _canvasHost.Add(Canvas);
            Canvas.schedule.Execute(() => Canvas.FitToView()).ExecuteLater(50);
        }

        private void UpdateStatus()
        {
            var model = Canvas.Model;
            _status.text = $"ノード {model.Nodes.Count} / 辺 {model.Edges.Count} / 選択 {Canvas.SelectedNodes.Count} / 戻す{Canvas.History.UndoCount}/進む{Canvas.History.RedoCount} / 縮尺 {Canvas.Scale:0.00}";
        }

        private static Label Hint()
        {
            var hint = new Label("左ドラッグ: 何も無い所=範囲選択・ノード=移動・出力ポート=接続・つながった入力ポート=付け替え・右下の角=大きさ　右/中ドラッグ: パン　ホイール: ズーム　Delete: 削除　Cmd/Ctrl+Z / Shift+Z: 元に戻す・やり直す　Shift+クリック: 選択の追加");
            hint.AddToClassList("ne-hint");
            return hint;
        }
    }
}
