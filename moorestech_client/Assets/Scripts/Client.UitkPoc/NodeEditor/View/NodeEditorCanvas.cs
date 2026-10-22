using System.Collections.Generic;
using Client.UitkPoc.Editing;
using Client.UitkPoc.NodeEditor.Model;
using UniRx;
using UnityEngine;
using UnityEngine.UIElements;

namespace Client.UitkPoc.NodeEditor.View
{
    // React Flow相当のキャンバス。モデルを購読してノード要素と辺を同期し、選択とパン・ズームを持つ
    // React Flow-like canvas: follows the model for nodes and edges, owns selection and pan/zoom
    public class NodeEditorCanvas : VisualElement
    {
        public const float GridSize = 20f;
        public const float MinScale = 0.1f;
        public const float MaxScale = 3f;

        public readonly NodeGraphModel Model;
        public readonly UndoStack History = new();
        public readonly HashSet<int> SelectedNodes = new();
        public readonly HashSet<int> MovingNodes = new();
        public readonly EdgeCanvasElement Edges;
        public readonly EdgeCanvasElement MovingEdges;
        public readonly VisualElement Marquee = new();
        private readonly VisualElement _world = new();
        private readonly GridBackgroundElement _grid = new(GridSize);
        private readonly MinimapElement _minimap;
        private readonly Dictionary<int, EditorNodeElement> _nodeElements = new();
        private bool _minimapDirty;

        public Vector2 Translation { get; private set; }
        public float Scale { get; private set; } = 1f;
        public bool SnapToGrid { get; private set; } = true;
        public int SelectedEdge { get; private set; } = -1;

        public NodeEditorCanvas(NodeGraphModel model)
        {
            Model = model;
            focusable = true;
            AddToClassList("ne-canvas");

            // 層の順: 方眼 → 内容（辺・ノード）→ 範囲選択の枠 → ミニマップ
            // Layer order: grid, content (edges, nodes), marquee, minimap
            _world.AddToClassList("ne-world");
            _world.usageHints = UsageHints.GroupTransform;
            _world.style.transformOrigin = new TransformOrigin(0, 0);
            Edges = new EdgeCanvasElement(model, MovingNodes, false);
            MovingEdges = new EdgeCanvasElement(model, MovingNodes, true);
            _world.Add(Edges);
            _world.Add(MovingEdges);
            Marquee.AddToClassList("ne-marquee");
            Marquee.pickingMode = PickingMode.Ignore;
            Marquee.style.display = DisplayStyle.None;
            _minimap = new MinimapElement(model, SelectedNodes);
            Add(_grid);
            Add(_world);
            Add(Marquee);
            Add(_minimap);

            foreach (var node in model.Nodes.Values) AddNodeElement(node);
            model.NodeAdded.Subscribe(AddNodeElement);
            model.NodeRemoved.Subscribe(RemoveNodeElement);
            model.NodeGeometryChanged.Subscribe(node => { _nodeElements[node.Id].Sync(node); OnNodeGeometryChanged(); });
            model.EdgesChanged.Subscribe(_ => { if (SelectedEdge >= 0 && !model.Edges.ContainsKey(SelectedEdge)) SelectEdge(-1); OnGraphChanged(); });
            _minimap.CenterRequested.Subscribe(CenterOn);
            RegisterCallback<GeometryChangedEvent>(_ => SetView(Translation, Scale));

            // ミニマップは0.1秒に1回までしか描き直さない。ドラッグ中に毎フレーム全ノードを描かないため
            // Repaint the minimap at most every 0.1 s so drags do not redraw every node each frame
            schedule.Execute(() => { if (_minimapDirty) _minimap.MarkDirtyRepaint(); _minimapDirty = false; }).Every(100);
            new NodeEditorInputController(this);
        }

        public void SetSnapToGrid(bool snap)
        {
            SnapToGrid = snap;
        }

        public void SetView(Vector2 translation, float scale)
        {
            Translation = translation;
            Scale = Mathf.Clamp(scale, MinScale, MaxScale);
            _world.style.translate = new Translate(Translation.x, Translation.y);
            _world.style.scale = new StyleScale(new Scale(new Vector3(Scale, Scale, 1f)));
            _grid.SetView(Translation, Scale);
            _minimap.SetViewport(new Rect(ViewportToContent(Vector2.zero), contentRect.size / Scale));
        }

        // 移動・大きさ変更の間だけ、対象ノードに付く辺を別の層へ移して毎フレームの描き直しをそこに限る
        // While moving or resizing, edges on those nodes move to their own layer so per-frame redraws stay small
        public void BeginNodeInteraction(IEnumerable<int> nodeIds)
        {
            MovingNodes.UnionWith(nodeIds);
            foreach (var id in MovingNodes) _nodeElements[id].BringToFront();
            Edges.MarkDirtyRepaint();
            MovingEdges.MarkDirtyRepaint();
        }

        public void EndNodeInteraction()
        {
            MovingNodes.Clear();
            Edges.MarkDirtyRepaint();
            MovingEdges.MarkDirtyRepaint();
        }

        public Vector2 ViewportToContent(Vector2 viewportPoint) => (viewportPoint - Translation) / Scale;
        public Vector2 ContentToViewport(Vector2 contentPoint) => contentPoint * Scale + Translation;

        public void SetNodeSelected(int nodeId, bool selected)
        {
            if (selected) SelectedNodes.Add(nodeId);
            else SelectedNodes.Remove(nodeId);
            _nodeElements[nodeId].SetSelected(selected);
            _minimap.MarkDirtyRepaint();
        }

        public void ClearNodeSelection()
        {
            foreach (var id in SelectedNodes) _nodeElements[id].SetSelected(false);
            SelectedNodes.Clear();
            _minimap.MarkDirtyRepaint();
        }

        public void SelectEdge(int edgeId)
        {
            SelectedEdge = edgeId;
            Edges.SetSelectedEdge(edgeId);
        }

        public void FitToView()
        {
            // 全ノードを包む矩形が余白付きで収まる縮尺と位置にする
            // Choose scale and offset so all nodes fit with a margin
            if (Model.Nodes.Count == 0) return;
            var bounds = new Rect();
            var first = true;
            foreach (var node in Model.Nodes.Values)
            {
                var rect = new Rect(node.Position, node.Size);
                bounds = first ? rect : Rect.MinMaxRect(Mathf.Min(bounds.xMin, rect.xMin), Mathf.Min(bounds.yMin, rect.yMin), Mathf.Max(bounds.xMax, rect.xMax), Mathf.Max(bounds.yMax, rect.yMax));
                first = false;
            }
            var size = contentRect.size;
            var scale = Mathf.Min((size.x - 80f) / bounds.width, (size.y - 80f) / bounds.height);
            scale = Mathf.Clamp(scale, MinScale, MaxScale);
            SetView(size * 0.5f - bounds.center * scale, scale);
        }

        private void CenterOn(Vector2 contentPoint)
        {
            SetView(contentRect.size * 0.5f - contentPoint * Scale, Scale);
        }

        private void AddNodeElement(EditorNode node)
        {
            var element = new EditorNodeElement(node);
            _nodeElements[node.Id] = element;
            _world.Add(element);
            OnGraphChanged();
        }

        private void RemoveNodeElement(int nodeId)
        {
            _nodeElements[nodeId].RemoveFromHierarchy();
            _nodeElements.Remove(nodeId);
            SelectedNodes.Remove(nodeId);
            OnGraphChanged();
        }

        private void OnNodeGeometryChanged()
        {
            if (MovingNodes.Count == 0) Edges.MarkDirtyRepaint();
            MovingEdges.MarkDirtyRepaint();
            _minimapDirty = true;
        }

        private void OnGraphChanged()
        {
            Edges.MarkDirtyRepaint();
            MovingEdges.MarkDirtyRepaint();
            _minimapDirty = true;
        }
    }
}
