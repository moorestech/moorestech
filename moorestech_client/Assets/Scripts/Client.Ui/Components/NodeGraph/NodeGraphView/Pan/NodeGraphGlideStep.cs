using Client.Ui.Foundation.Motion.Inertia;
using UnityEngine;

namespace Client.Ui.Components.NodeGraph
{
    /// <summary>
    /// 1 次元の滑走の変位を、離したときの向きに載せてパンする
    /// Pans by the 1-D glide displacement along the release direction
    /// </summary>
    internal sealed class NodeGraphGlideStep : IUiInertiaStep
    {
        private readonly NodeGraphView _view;
        private readonly Vector2 _direction;
        private float _appliedOffset;

        public NodeGraphGlideStep(NodeGraphView view, Vector2 direction)
        {
            _view = view;
            _direction = direction;
        }

        public void Apply(float offset)
        {
            // 開始からの変位を受け、前回からの差だけ動かす（操作のパンと足し合わせられる）
            // Receives the displacement since start and moves only the change since last time (so it composes with manual panning)
            _view.PanBy(_direction * (offset - _appliedOffset));
            _appliedOffset = offset;
        }
    }
}
