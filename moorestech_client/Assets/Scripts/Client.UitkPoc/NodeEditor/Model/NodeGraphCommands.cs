using System.Collections.Generic;
using System.Linq;
using Client.UitkPoc.Editing;
using UnityEngine;

namespace Client.UitkPoc.NodeEditor.Model
{
    public class AddNodeCommand : IUndoableCommand
    {
        private readonly NodeGraphModel _model;
        private readonly EditorNode _node;

        public AddNodeCommand(NodeGraphModel model, EditorNode node)
        {
            _model = model;
            _node = node;
        }

        public void Do() => _model.AddNode(_node);
        public void Undo() => _model.RemoveNode(_node.Id);
    }

    public class ConnectCommand : IUndoableCommand
    {
        private readonly NodeGraphModel _model;
        private readonly EditorEdge _edge;

        public ConnectCommand(NodeGraphModel model, EditorEdge edge)
        {
            _model = model;
            _edge = edge;
        }

        public void Do() => _model.AddEdge(_edge);
        public void Undo() => _model.RemoveEdge(_edge.Id);
    }

    public class DisconnectCommand : IUndoableCommand
    {
        private readonly NodeGraphModel _model;
        private readonly EditorEdge _edge;

        public DisconnectCommand(NodeGraphModel model, EditorEdge edge)
        {
            _model = model;
            _edge = edge;
        }

        public void Do() => _model.RemoveEdge(_edge.Id);
        public void Undo() => _model.AddEdge(_edge);
    }

    // ドラッグで動かしたノード群の前後位置を持つ。ドラッグ中に適用済みなのでRecordで積む
    // Holds before/after positions of dragged nodes; applied during the drag, so pushed with Record
    public class MoveNodesCommand : IUndoableCommand
    {
        private readonly NodeGraphModel _model;
        private readonly Dictionary<int, Vector2> _before;
        private readonly Dictionary<int, Vector2> _after;

        public MoveNodesCommand(NodeGraphModel model, Dictionary<int, Vector2> before, Dictionary<int, Vector2> after)
        {
            _model = model;
            _before = before;
            _after = after;
        }

        public void Do()
        {
            foreach (var (id, position) in _after) _model.SetNodePosition(id, position);
        }

        public void Undo()
        {
            foreach (var (id, position) in _before) _model.SetNodePosition(id, position);
        }
    }

    public class ResizeNodeCommand : IUndoableCommand
    {
        private readonly NodeGraphModel _model;
        private readonly int _nodeId;
        private readonly Vector2 _before;
        private readonly Vector2 _after;

        public ResizeNodeCommand(NodeGraphModel model, int nodeId, Vector2 before, Vector2 after)
        {
            _model = model;
            _nodeId = nodeId;
            _before = before;
            _after = after;
        }

        public void Do() => _model.SetNodeSize(_nodeId, _after);
        public void Undo() => _model.SetNodeSize(_nodeId, _before);
    }

    // 選択したノード・辺の削除。ノードにつながる辺も一緒に消し、戻すときはノードを先に復元する
    // Deletes selected nodes and edges plus edges attached to those nodes; undo restores nodes first
    public class DeleteSelectionCommand : IUndoableCommand
    {
        private readonly NodeGraphModel _model;
        private readonly List<EditorNode> _nodes;
        private readonly List<EditorEdge> _edges;

        public DeleteSelectionCommand(NodeGraphModel model, IEnumerable<int> nodeIds, IEnumerable<int> edgeIds)
        {
            _model = model;
            _nodes = nodeIds.Select(id => model.Nodes[id]).ToList();
            var nodeSet = new HashSet<int>(_nodes.Select(n => n.Id));
            var edgeSet = new HashSet<int>(edgeIds);
            _edges = model.Edges.Values.Where(e => edgeSet.Contains(e.Id) || nodeSet.Contains(e.FromNode) || nodeSet.Contains(e.ToNode)).ToList();
        }

        public void Do()
        {
            foreach (var edge in _edges) _model.RemoveEdge(edge.Id);
            foreach (var node in _nodes) _model.RemoveNode(node.Id);
        }

        public void Undo()
        {
            foreach (var node in _nodes) _model.AddNode(node);
            foreach (var edge in _edges) _model.AddEdge(edge);
        }
    }
}
