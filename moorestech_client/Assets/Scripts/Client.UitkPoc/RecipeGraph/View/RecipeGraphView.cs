using System;
using System.Collections.Generic;
using System.Linq;
using Client.UitkPoc.RecipeGraph.Layout;
using Client.UitkPoc.RecipeGraph.Model;
using Client.UitkPoc.Shell;
using UniRx;
using UnityEngine;
using UnityEngine.UIElements;

namespace Client.UitkPoc.RecipeGraph.View
{
    // パン・ズームできるキャンバスに辺・ノード・ラベルを重ね、再計算時は差分のノードを再利用して滑らせる
    // Pan/zoom canvas stacking edges, nodes and labels; on recompute it reuses nodes and slides them
    public class RecipeGraphView : VisualElement, IRecipeNodeDragReceiver
    {
        public readonly Subject<string> RecipeCycleRequested = new();

        private readonly UitkPocContext _context;
        private readonly VisualElement _canvas = new();
        private readonly RecipeEdgeLayer _edgeLayer = new();
        private readonly VisualElement _nodeLayer = new();
        private readonly EdgeLabelLayer _labelLayer = new();
        private readonly PanZoomManipulator _panZoom;
        private readonly NodePositionTween _tween = new();
        private readonly Dictionary<string, RecipeNodeElement> _elementsByGuid = new();
        private readonly Dictionary<string, Vector2> _positionByGuid = new();
        private ProductionGraph _graph;
        private LayoutResult _layout;
        private RecipeEdgeRouter _router;
        private RecipeNodeElement[] _nodeElements = Array.Empty<RecipeNodeElement>();
        private Vector2[] _positions = Array.Empty<Vector2>();
        private bool[] _manuallyMoved = Array.Empty<bool>();
        private bool _fitPending = true;

        public RecipeGraphView(UitkPocContext context)
        {
            _context = context;
            AddToClassList("rg-viewport");
            _canvas.AddToClassList("rg-canvas");
            _nodeLayer.pickingMode = PickingMode.Ignore;
            _canvas.Add(_edgeLayer);
            _canvas.Add(_labelLayer);
            _canvas.Add(_nodeLayer);
            Add(_canvas);
            _panZoom = new PanZoomManipulator(_canvas);
            this.AddManipulator(_panZoom);
            schedule.Execute(timer => Tick(timer.deltaTime / 1000f)).Every(0);
            RegisterCallback<GeometryChangedEvent>(_ => { if (_fitPending) FitToView(); });
        }

        public void SetGraph(ProductionGraph graph, LayoutResult layout)
        {
            _graph = graph;
            _layout = layout;
            _router = new RecipeEdgeRouter(graph.Edges.Count);
            var from = new Vector2[graph.Nodes.Count];
            var alive = new HashSet<string>();
            _nodeElements = new RecipeNodeElement[graph.Nodes.Count];
            for (var i = 0; i < graph.Nodes.Count; i++) BindNode(i);

            // 消えたノードはフェードしてから外す
            // Fade out removed nodes before detaching
            foreach (var guid in _elementsByGuid.Keys.Where(g => !alive.Contains(g)).ToList())
            {
                var leaving = _elementsByGuid[guid];
                leaving.AddToClassList("rg-node--leaving");
                leaving.pickingMode = PickingMode.Ignore;
                leaving.schedule.Execute(leaving.RemoveFromHierarchy).ExecuteLater(300);
                _elementsByGuid.Remove(guid);
            }

            _positions = (Vector2[])from.Clone();
            _manuallyMoved = new bool[graph.Nodes.Count];
            _edgeLayer.style.width = layout.Size.x + 400;
            _edgeLayer.style.height = layout.Size.y + 400;
            _labelLayer.Rebuild(graph);
            _tween.Start(from, layout.NodePositions);
            SetFocus(null);

            #region Internal

            void BindNode(int i)
            {
                var node = graph.Nodes[i];
                alive.Add(node.ResourceGuid);
                if (_elementsByGuid.TryGetValue(node.ResourceGuid, out var element))
                {
                    element.SetNumbers(node);
                    from[i] = _positionByGuid[node.ResourceGuid];
                }
                else
                {
                    // 新規ノードは最終位置に透明で置き、次フレームでフェードインさせる
                    // New nodes appear transparent at their final spot and fade in next frame
                    element = new RecipeNodeElement(node, _context.LoadItemIcon(node.ResourceName));
                    element.AddToClassList("rg-node--entering");
                    element.schedule.Execute(() => element.RemoveFromClassList("rg-node--entering")).ExecuteLater(16);
                    element.AddManipulator(new NodeDragManipulator(this, element));
                    element.RegisterCallback<PointerEnterEvent>(_ => SetFocus(element));
                    element.RegisterCallback<PointerLeaveEvent>(_ => SetFocus(null));
                    _nodeLayer.Add(element);
                    _elementsByGuid[node.ResourceGuid] = element;
                    from[i] = layout.NodePositions[i];
                }
                _nodeElements[i] = element;
            }

            #endregion
        }

        public void ResetManualPositions()
        {
            _manuallyMoved = new bool[_positions.Length];
            _tween.Start((Vector2[])_positions.Clone(), _layout.NodePositions);
        }

        public void SetFlowAnimation(bool enabled)
        {
            _edgeLayer.SetFlowAnimation(enabled);
        }

        public void FitToView()
        {
            var viewport = contentRect.size;
            if (_layout == null || viewport.x < 1f) return;
            var scale = Mathf.Min((viewport.x - 80f) / _layout.Size.x, (viewport.y - 80f) / _layout.Size.y, 1.1f);
            _panZoom.SetView((viewport - _layout.Size * scale) / 2f, scale);
            _fitPending = false;
        }

        public void DragNode(RecipeNodeElement node, Vector2 panelDelta)
        {
            var index = Array.IndexOf(_nodeElements, node);
            _positions[index] += panelDelta / _panZoom.Scale;
            _manuallyMoved[index] = true;
            ApplyPositions();
        }

        public void ClickNode(RecipeNodeElement node, VisualElement clickedElement)
        {
            if (clickedElement == node.AlternativeBadge) RecipeCycleRequested.OnNext(node.ResourceGuid);
        }

        private void Tick(float deltaSeconds)
        {
            if (_graph == null) return;
            if (_tween.IsRunning)
            {
                _tween.Advance(deltaSeconds, _positions);
                ApplyPositions();
            }
            _edgeLayer.AdvanceFlow(deltaSeconds);

            // 遠景では辺ラベルを隠す（詳細度の切替）
            // Hide edge labels when zoomed out (level of detail)
            _canvas.EnableInClassList("rg-canvas--far", _panZoom.Scale < 0.55f);
        }

        private void ApplyPositions()
        {
            for (var i = 0; i < _nodeElements.Length; i++)
            {
                _nodeElements[i].style.translate = new Translate(_positions[i].x, _positions[i].y);
                _positionByGuid[_nodeElements[i].ResourceGuid] = _positions[i];
            }

            // 辺を引き直し、ラベルを各辺の中点へ動かす
            // Reroute edges and move labels to each edge midpoint
            _router.Route(_graph, _positions, _layout.EdgeWaypoints, _manuallyMoved);
            _edgeLayer.SetGraph(_graph, _router);
            _labelLayer.Place(_router.LabelAnchors);
        }

        private void SetFocus(RecipeNodeElement focus)
        {
            // フォーカスの上流・下流以外を暗くする
            // Dim everything outside the focus's upstream and downstream
            var related = focus == null ? default : RecipeGraphHighlight.Collect(_graph, Array.IndexOf(_nodeElements, focus));
            for (var i = 0; i < _nodeElements.Length; i++) _nodeElements[i].EnableInClassList("rg-node--dim", focus != null && !related.nodes.Contains(i));
            _labelLayer.SetDimmed(focus == null ? null : related.edges);
            _edgeLayer.SetHighlightedEdges(focus == null ? null : related.edges);
        }
    }
}
