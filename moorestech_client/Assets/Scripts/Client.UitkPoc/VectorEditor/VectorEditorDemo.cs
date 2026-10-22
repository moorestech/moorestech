using System.Collections.Generic;
using System.Linq;
using Client.UitkPoc.Shell;
using Client.UitkPoc.VectorEditor.View;
using UniRx;
using UnityEngine;
using UnityEngine.UIElements;

namespace Client.UitkPoc.VectorEditor
{
    // Figma風の検証。図形ツール・ペン・変形の取っ手・スマートガイド・元に戻すと、図形数・ズーム倍率での負荷と画質
    // Figma-like checks: shape tools, pen, transform handles, smart guides, undo, plus load and quality by count and zoom
    public class VectorEditorDemo : IUitkPocDemo
    {
        private static readonly int[] ShapeCounts = { 12, 1000, 5000 };

        private readonly Dictionary<VectorTool, Button> _toolButtons = new();
        private VisualElement _canvasHost;
        private Toggle _redrawToggle;
        private Label _status;

        public string Title => "ベクター";
        public VectorCanvas Canvas { get; private set; }

        public VectorEditorDemo(UitkPocContext context)
        {
        }

        public VisualElement CreateView()
        {
            var root = new VisualElement();
            root.styleSheets.Add(Resources.Load<StyleSheet>("UitkPoc/VectorEditor"));
            root.AddToClassList("ve-root");
            var toolbar = new VisualElement();
            toolbar.AddToClassList("ve-toolbar");
            AddToolButton(toolbar, VectorTool.Select, "選択 V");
            AddToolButton(toolbar, VectorTool.Rectangle, "矩形 R");
            AddToolButton(toolbar, VectorTool.Ellipse, "楕円 O");
            AddToolButton(toolbar, VectorTool.Pen, "ペン P");
            var count = new DropdownField("図形数", ShapeCounts.Select(c => c.ToString()).ToList(), 0);
            count.RegisterValueChangedCallback(_ => Rebuild(ShapeCounts[count.index]));
            _redrawToggle = new Toggle("拡大時に描き直す") { value = true };
            _redrawToggle.RegisterValueChangedCallback(evt => Canvas.SetRedrawOnZoom(evt.newValue));
            toolbar.Add(count);
            toolbar.Add(_redrawToggle);
            toolbar.Add(new Button(() => Canvas.History.Undo()) { text = "元に戻す" });
            toolbar.Add(new Button(() => Canvas.History.Redo()) { text = "やり直す" });
            toolbar.Add(new Button(() => SetZoomAtCenter(1f)) { text = "100%" });
            toolbar.Add(new Button(() => SetZoomAtCenter(10f)) { text = "1000%" });
            _status = new Label();
            _status.AddToClassList("ve-status");
            toolbar.Add(_status);
            root.Add(toolbar);
            _canvasHost = new VisualElement();
            _canvasHost.AddToClassList("ve-canvas-host");
            root.Add(_canvasHost);
            var hint = new Label("V/R/O/P: ツール切替　ペン: クリック=角・ドラッグ=曲線・最初の点=閉じる・Enter=確定　Shift: 正方形/縦横比固定/15°刻み/選択の追加　右/中ドラッグ: パン　ホイール: ズーム　Delete: 削除　Cmd/Ctrl+Z / Shift+Z");
            hint.AddToClassList("ve-hint");
            root.Add(hint);
            Rebuild(ShapeCounts[0]);
            root.schedule.Execute(UpdateStatus).Every(200);
            return root;
        }

        public void Rebuild(int shapeCount)
        {
            _canvasHost.Clear();
            Canvas = new VectorCanvas(VectorSampleFactory.Create(shapeCount));
            Canvas.SetRedrawOnZoom(_redrawToggle.value);
            Canvas.ToolChanged.Subscribe(HighlightTool);
            _canvasHost.Add(Canvas);
            HighlightTool(Canvas.Tool);
        }

        public void SetZoomAtCenter(float zoom)
        {
            Canvas.ZoomAt(Canvas.contentRect.size * 0.5f, zoom);
        }

        private void AddToolButton(VisualElement toolbar, VectorTool tool, string label)
        {
            var button = new Button(() => Canvas.SetTool(tool)) { text = label };
            button.AddToClassList("ve-tool");
            _toolButtons[tool] = button;
            toolbar.Add(button);
        }

        private void HighlightTool(VectorTool active)
        {
            foreach (var (tool, button) in _toolButtons) button.EnableInClassList("ve-tool--active", tool == active);
        }

        private void UpdateStatus()
        {
            var layer = Canvas.StaticLayer;
            _status.text = $"図形 {Canvas.Document.Shapes.Count} / 描画 {layer.DrawnCount}（描画縮尺 {layer.DrawScale:0.##}・最終描画 {layer.LastDrawMilliseconds:0.0}ms・操作層 {Canvas.ActiveLayer.LastDrawMilliseconds:0.0}ms）/ 選択 {Canvas.Selected.Count} / 戻す{Canvas.History.UndoCount}/進む{Canvas.History.RedoCount} / ズーム {Canvas.Zoom * 100f:0}%";
        }
    }
}
