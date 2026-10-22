using System.Collections.Generic;
using Client.UitkPoc.Editing;
using Client.UitkPoc.NodeEditor.Model;
using UnityEngine;
using UnityEngine.UIElements;

namespace Client.UitkPoc.NodeEditor.View
{
    // 出力ポートから入力ポートへ線を引く1回分のドラッグ。入力ポートから掴んだ場合は既存の辺を外して付け替える
    // One drag drawing an edge from an output to an input; grabbing a connected input detaches its edge for reconnection
    public class ConnectionDragSession
    {
        private readonly NodeEditorCanvas _canvas;
        private readonly int _fromNode;
        private readonly int _fromPort;
        private readonly EditorEdge _detachedEdge;
        private VisualElement _hoveredPort;

        public ConnectionDragSession(NodeEditorCanvas canvas, PortRef pressedPort)
        {
            _canvas = canvas;
            var model = canvas.Model;
            if (pressedPort.IsInput)
            {
                // 付け替え: 辺をいったんモデルから外し、その出力側から引き直す
                // Reconnect: lift the edge out of the model and redraw it from its output side
                _detachedEdge = model.FindEdgeIntoPort(pressedPort.NodeId, pressedPort.Index);
                if (_detachedEdge == null) return;
                model.RemoveEdge(_detachedEdge.Id);
                _fromNode = _detachedEdge.FromNode;
                _fromPort = _detachedEdge.FromPort;
                return;
            }
            _fromNode = pressedPort.NodeId;
            _fromPort = pressedPort.Index;
        }

        public bool IsValid => _fromNode != 0;

        public void Move(Vector2 panelPosition, Vector2 contentPoint)
        {
            var from = EditorNodeElement.PortPosition(_canvas.Model.Nodes[_fromNode], false, _fromPort);
            var target = FindDropPort(panelPosition);
            _hoveredPort?.RemoveFromClassList("ne-port--target");
            _hoveredPort = target;
            _hoveredPort?.AddToClassList("ne-port--target");
            var to = target != null ? EditorNodeElement.PortPosition(_canvas.Model.Nodes[((PortRef)target.userData).NodeId], true, ((PortRef)target.userData).Index) : contentPoint;
            _canvas.MovingEdges.SetPreview(true, from, to);
        }

        public void Finish(Vector2 panelPosition)
        {
            _hoveredPort?.RemoveFromClassList("ne-port--target");
            _canvas.MovingEdges.SetPreview(false, Vector2.zero, Vector2.zero);
            var model = _canvas.Model;

            // 外した辺は一度戻し、確定内容（外す・既存を置き換える・つなぐ）を1つの取り消し単位で実行する
            // Restore the lifted edge, then apply the outcome (detach, replace, connect) as one undo step
            if (_detachedEdge != null) model.AddEdge(_detachedEdge);
            var commands = new List<IUndoableCommand>();
            var target = FindDropPort(panelPosition);
            var targetRef = target?.userData as PortRef;
            if (_detachedEdge != null && targetRef != null && targetRef.NodeId == _detachedEdge.ToNode && targetRef.Index == _detachedEdge.ToPort) return;
            if (_detachedEdge != null) commands.Add(new DisconnectCommand(model, _detachedEdge));
            if (targetRef != null)
            {
                var occupied = model.FindEdgeIntoPort(targetRef.NodeId, targetRef.Index);
                if (occupied != null && occupied != _detachedEdge) commands.Add(new DisconnectCommand(model, occupied));
                commands.Add(new ConnectCommand(model, new EditorEdge(model.AllocateId(), _fromNode, _fromPort, targetRef.NodeId, targetRef.Index)));
            }
            if (commands.Count > 0) _canvas.History.Execute(new CompositeCommand(commands));
        }

        private VisualElement FindDropPort(Vector2 panelPosition)
        {
            // 自分のノード以外の入力ポートだけを落とし先として認める
            // Only input ports on other nodes are accepted as drop targets
            var picked = _canvas.panel.Pick(panelPosition);
            while (picked != null && picked.userData is not PortRef) picked = picked.parent;
            return picked?.userData is PortRef port && port.IsInput && port.NodeId != _fromNode ? picked : null;
        }
    }
}
