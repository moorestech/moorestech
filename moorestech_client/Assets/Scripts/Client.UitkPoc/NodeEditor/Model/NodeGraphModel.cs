using System.Collections.Generic;
using UniRx;
using UnityEngine;

namespace Client.UitkPoc.NodeEditor.Model
{
    public class EditorNode
    {
        public readonly int Id;
        public readonly string Title;
        public readonly int InputCount;
        public readonly int OutputCount;
        public Vector2 Position { get; private set; }
        public Vector2 Size { get; private set; }

        public EditorNode(int id, string title, int inputCount, int outputCount, Vector2 position, Vector2 size)
        {
            Id = id;
            Title = title;
            InputCount = inputCount;
            OutputCount = outputCount;
            Position = position;
            Size = size;
        }

        public void SetPosition(Vector2 position)
        {
            Position = position;
        }

        public void SetSize(Vector2 size)
        {
            Size = size;
        }
    }

    public class EditorEdge
    {
        public readonly int Id;
        public readonly int FromNode;
        public readonly int FromPort;
        public readonly int ToNode;
        public readonly int ToPort;

        public EditorEdge(int id, int fromNode, int fromPort, int toNode, int toPort)
        {
            Id = id;
            FromNode = fromNode;
            FromPort = fromPort;
            ToNode = toNode;
            ToPort = toPort;
        }
    }

    // ノードと辺の正本。変更はすべてここを通し、ビューは購読で追従する
    // Source of truth for nodes and edges; all edits go through here and views follow via subscriptions
    public class NodeGraphModel
    {
        public readonly Dictionary<int, EditorNode> Nodes = new();
        public readonly Dictionary<int, EditorEdge> Edges = new();

        private readonly Subject<EditorNode> _nodeAdded = new();
        private readonly Subject<int> _nodeRemoved = new();
        private readonly Subject<EditorNode> _nodeGeometryChanged = new();
        private readonly Subject<Unit> _edgesChanged = new();
        private int _nextId = 1;

        public System.IObservable<EditorNode> NodeAdded => _nodeAdded;
        public System.IObservable<int> NodeRemoved => _nodeRemoved;
        public System.IObservable<EditorNode> NodeGeometryChanged => _nodeGeometryChanged;
        public System.IObservable<Unit> EdgesChanged => _edgesChanged;

        public int AllocateId()
        {
            return _nextId++;
        }

        public void AddNode(EditorNode node)
        {
            Nodes[node.Id] = node;
            _nodeAdded.OnNext(node);
        }

        public void RemoveNode(int nodeId)
        {
            Nodes.Remove(nodeId);
            _nodeRemoved.OnNext(nodeId);
        }

        public void SetNodePosition(int nodeId, Vector2 position)
        {
            var node = Nodes[nodeId];
            node.SetPosition(position);
            _nodeGeometryChanged.OnNext(node);
        }

        public void SetNodeSize(int nodeId, Vector2 size)
        {
            var node = Nodes[nodeId];
            node.SetSize(size);
            _nodeGeometryChanged.OnNext(node);
        }

        public void AddEdge(EditorEdge edge)
        {
            Edges[edge.Id] = edge;
            _edgesChanged.OnNext(Unit.Default);
        }

        public void RemoveEdge(int edgeId)
        {
            Edges.Remove(edgeId);
            _edgesChanged.OnNext(Unit.Default);
        }

        public EditorEdge FindEdgeIntoPort(int toNode, int toPort)
        {
            foreach (var edge in Edges.Values) if (edge.ToNode == toNode && edge.ToPort == toPort) return edge;
            return null;
        }
    }
}
