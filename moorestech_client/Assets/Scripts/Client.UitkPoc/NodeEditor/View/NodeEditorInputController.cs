using System.Collections.Generic;
using System.Linq;
using Client.UitkPoc.NodeEditor.Model;
using UnityEngine;
using UnityEngine.UIElements;

namespace Client.UitkPoc.NodeEditor.View
{
    // キャンバス上のポインタ操作を1か所で振り分ける。押した場所で、パン・範囲選択・移動・大きさ変更・接続のどれかになる
    // Routes all pointer input on the canvas; the press location decides pan, marquee, move, resize or connect
    public class NodeEditorInputController
    {
        private enum Mode { None, Pan, Marquee, Move, Resize, Connect }

        private const float DragThreshold = 3f;

        private readonly NodeEditorCanvas _canvas;
        private Mode _mode;
        private Vector2 _pressLocal;
        private bool _movedPastThreshold;
        private int _primaryNode;
        private Dictionary<int, Vector2> _startPositions;
        private Vector2 _startSize;
        private HashSet<int> _selectionBeforeMarquee;
        private ConnectionDragSession _connection;

        public NodeEditorInputController(NodeEditorCanvas canvas)
        {
            _canvas = canvas;
            canvas.RegisterCallback<PointerDownEvent>(OnPointerDown);
            canvas.RegisterCallback<PointerMoveEvent>(OnPointerMove);
            canvas.RegisterCallback<PointerUpEvent>(OnPointerUp);
            canvas.RegisterCallback<WheelEvent>(OnWheel);
            canvas.RegisterCallback<KeyDownEvent>(OnKeyDown);
        }

        private void OnPointerDown(PointerDownEvent evt)
        {
            _canvas.Focus();
            _pressLocal = _canvas.WorldToLocal(evt.position);
            _movedPastThreshold = false;
            _mode = evt.button != 0 ? Mode.Pan : ClassifyPress((VisualElement)evt.target, evt.shiftKey, evt.position);
            if (_mode == Mode.None) return;
            _canvas.CapturePointer(evt.pointerId);
            evt.StopPropagation();
        }

        private Mode ClassifyPress(VisualElement target, bool shift, Vector2 panelPosition)
        {
            if (target.userData is PortRef port)
            {
                _connection = new ConnectionDragSession(_canvas, port);
                return _connection.IsValid ? Mode.Connect : Mode.None;
            }
            var node = target as EditorNodeElement ?? target.GetFirstAncestorOfType<EditorNodeElement>();
            if (node != null)
            {
                _primaryNode = node.NodeId;
                node.BringToFront();
                if (target.ClassListContains("ne-node__grip"))
                {
                    _startSize = _canvas.Model.Nodes[node.NodeId].Size;
                    return Mode.Resize;
                }
                SelectOnPress(node.NodeId, shift);
                _startPositions = _canvas.SelectedNodes.ToDictionary(id => id, id => _canvas.Model.Nodes[id].Position);
                return Mode.Move;
            }

            // 何も無い所: 辺に当たれば辺を選び、外れなら範囲選択を始める
            // Empty space: select an edge on hit, otherwise start a marquee
            var edge = _canvas.Edges.HitTest(_canvas.ViewportToContent(_canvas.WorldToLocal(panelPosition)), 6f / _canvas.Scale);
            if (edge >= 0)
            {
                _canvas.ClearNodeSelection();
                _canvas.SelectEdge(edge);
                return Mode.None;
            }
            _selectionBeforeMarquee = shift ? new HashSet<int>(_canvas.SelectedNodes) : new HashSet<int>();
            return Mode.Marquee;
        }

        private void SelectOnPress(int nodeId, bool shift)
        {
            // Shiftで追加・解除、そうでなければ未選択のノードだけを単独選択にする（選択済みなら複数のまま動かす）
            // Shift toggles; otherwise an unselected node becomes the sole selection (selected ones keep the group)
            _canvas.SelectEdge(-1);
            if (shift)
            {
                _canvas.SetNodeSelected(nodeId, !_canvas.SelectedNodes.Contains(nodeId));
                return;
            }
            if (_canvas.SelectedNodes.Contains(nodeId)) return;
            _canvas.ClearNodeSelection();
            _canvas.SetNodeSelected(nodeId, true);
        }

        private void OnPointerMove(PointerMoveEvent evt)
        {
            if (_mode == Mode.None || !_canvas.HasPointerCapture(evt.pointerId)) return;
            var local = _canvas.WorldToLocal(evt.position);
            _movedPastThreshold |= Vector2.Distance(local, _pressLocal) > DragThreshold;
            var contentDelta = (local - _pressLocal) / _canvas.Scale;
            if (_mode == Mode.Pan) _canvas.SetView(_canvas.Translation + (Vector2)evt.deltaPosition, _canvas.Scale);
            if (_mode == Mode.Connect) _connection.Move(evt.position, _canvas.ViewportToContent(local));
            if (_mode == Mode.Marquee && _movedPastThreshold) UpdateMarquee(local, evt.shiftKey);
            if (_mode is Mode.Move or Mode.Resize && _movedPastThreshold && _canvas.MovingNodes.Count == 0)
                _canvas.BeginNodeInteraction(_mode == Mode.Move ? new List<int>(_startPositions.Keys) : new List<int> { _primaryNode });
            if (_mode == Mode.Move && _movedPastThreshold) MoveSelection(contentDelta);
            if (_mode == Mode.Resize && _movedPastThreshold) Resize(contentDelta);
            evt.StopPropagation();
        }

        private void OnPointerUp(PointerUpEvent evt)
        {
            if (_mode == Mode.None) return;
            _canvas.ReleasePointer(evt.pointerId);
            var model = _canvas.Model;
            if (_mode == Mode.Connect) _connection.Finish(evt.position);
            if (_mode == Mode.Marquee)
            {
                _canvas.Marquee.style.display = DisplayStyle.None;
                if (!_movedPastThreshold && !evt.shiftKey) { _canvas.ClearNodeSelection(); _canvas.SelectEdge(-1); }
            }
            if (_mode == Mode.Move && _movedPastThreshold)
                _canvas.History.Record(new MoveNodesCommand(model, _startPositions, _startPositions.Keys.ToDictionary(id => id, id => model.Nodes[id].Position)));
            if (_mode == Mode.Resize && _movedPastThreshold)
                _canvas.History.Record(new ResizeNodeCommand(model, _primaryNode, _startSize, model.Nodes[_primaryNode].Size));
            if (_canvas.MovingNodes.Count > 0) _canvas.EndNodeInteraction();
            _mode = Mode.None;
            evt.StopPropagation();
        }

        private void MoveSelection(Vector2 contentDelta)
        {
            // 掴んだノードの位置を方眼に吸着させ、そのずれを選択中の全ノードへ同じだけ適用する
            // Snap the grabbed node to the grid and apply the same offset to every selected node
            var target = _startPositions[_primaryNode] + contentDelta;
            if (_canvas.SnapToGrid) target = Snap(target);
            var offset = target - _startPositions[_primaryNode];
            foreach (var (id, start) in _startPositions) _canvas.Model.SetNodePosition(id, start + offset);
        }

        private void Resize(Vector2 contentDelta)
        {
            var node = _canvas.Model.Nodes[_primaryNode];
            var size = _startSize + contentDelta;
            if (_canvas.SnapToGrid) size = Snap(size);
            _canvas.Model.SetNodeSize(_primaryNode, new Vector2(Mathf.Max(EditorNodeElement.MinWidth, size.x), Mathf.Max(EditorNodeElement.MinHeight(node), size.y)));
        }

        private void UpdateMarquee(Vector2 local, bool shift)
        {
            // ビュー座標の枠と、ビュー座標へ写したノード矩形の重なりで選ぶ
            // Select by overlap between the viewport-space frame and node rects mapped to viewport space
            var rect = Rect.MinMaxRect(Mathf.Min(local.x, _pressLocal.x), Mathf.Min(local.y, _pressLocal.y), Mathf.Max(local.x, _pressLocal.x), Mathf.Max(local.y, _pressLocal.y));
            var marquee = _canvas.Marquee;
            marquee.style.display = DisplayStyle.Flex;
            marquee.style.left = rect.x;
            marquee.style.top = rect.y;
            marquee.style.width = rect.width;
            marquee.style.height = rect.height;
            foreach (var node in _canvas.Model.Nodes.Values)
            {
                var nodeRect = new Rect(_canvas.ContentToViewport(node.Position), node.Size * _canvas.Scale);
                var selected = _selectionBeforeMarquee.Contains(node.Id) || rect.Overlaps(nodeRect);
                if (selected != _canvas.SelectedNodes.Contains(node.Id)) _canvas.SetNodeSelected(node.Id, selected);
            }
        }

        private void OnWheel(WheelEvent evt)
        {
            var cursor = _canvas.WorldToLocal(evt.mousePosition);
            var newScale = Mathf.Clamp(_canvas.Scale * Mathf.Exp(-evt.delta.y * 0.08f), NodeEditorCanvas.MinScale, NodeEditorCanvas.MaxScale);
            var anchor = _canvas.ViewportToContent(cursor);
            _canvas.SetView(cursor - anchor * newScale, newScale);
            evt.StopPropagation();
        }

        private void OnKeyDown(KeyDownEvent evt)
        {
            var model = _canvas.Model;
            if (evt.keyCode is KeyCode.Delete or KeyCode.Backspace && (_canvas.SelectedNodes.Count > 0 || _canvas.SelectedEdge >= 0))
            {
                var edges = _canvas.SelectedEdge >= 0 ? new[] { _canvas.SelectedEdge } : new int[0];
                _canvas.History.Execute(new DeleteSelectionCommand(model, _canvas.SelectedNodes.ToList(), edges));
            }
            if (evt.actionKey && evt.keyCode == KeyCode.Z && !evt.shiftKey) _canvas.History.Undo();
            if (evt.actionKey && (evt.keyCode == KeyCode.Y || (evt.keyCode == KeyCode.Z && evt.shiftKey))) _canvas.History.Redo();
            if (evt.actionKey && evt.keyCode == KeyCode.A) foreach (var id in model.Nodes.Keys) _canvas.SetNodeSelected(id, true);
        }

        private static Vector2 Snap(Vector2 value)
        {
            return new Vector2(Mathf.Round(value.x / NodeEditorCanvas.GridSize), Mathf.Round(value.y / NodeEditorCanvas.GridSize)) * NodeEditorCanvas.GridSize;
        }
    }
}
