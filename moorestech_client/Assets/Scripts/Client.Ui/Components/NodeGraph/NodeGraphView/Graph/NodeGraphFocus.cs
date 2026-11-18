using UnityEngine;

namespace Client.Ui.Components.NodeGraph
{
    /// <summary>
    /// 初回に中央へ据える点の有無（Web の initialFocus: TreePoint | null）
    /// Whether a point is centered on first show (the Web initialFocus: TreePoint | null)
    /// </summary>
    public readonly struct NodeGraphFocus
    {
        private readonly Vector2 _treePoint;
        private readonly bool _hasPoint;

        private NodeGraphFocus(Vector2 treePoint, bool hasPoint)
        {
            _treePoint = treePoint;
            _hasPoint = hasPoint;
        }

        public static NodeGraphFocus None => new(Vector2.zero, false);

        public static NodeGraphFocus At(Vector2 treePoint)
        {
            return new NodeGraphFocus(treePoint, true);
        }

        public bool TryGetPoint(out Vector2 treePoint)
        {
            treePoint = _treePoint;
            return _hasPoint;
        }
    }
}
