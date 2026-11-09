using System;
using System.Collections.Generic;
using UniRx;
using UnityEngine.UIElements;

namespace Client.Ui.Screens.Tutorial.Anchor
{
    /// <summary>
    /// 購読中のアンカーと祖先の GeometryChangedEvent を見張り、レイアウト後に測り直す合図を出す
    /// Watches GeometryChangedEvent on subscribed anchors and their ancestors to signal a post-layout re-measure
    /// </summary>
    /// <remarks>
    /// パネルのスケジューラはスタイル解決・レイアウトより先に走るので、毎フレームの測定だけでは移動・display: none が 1 フレーム遅れる。
    /// GeometryChangedEvent はレイアウト段で送られるので、ここから測ればそのフレームの描画に間に合う。イベントは伝播しないため要素ごとに張る。
    /// The panel scheduler runs before style resolution and layout, so per-frame measuring alone lags moves and display: none by a frame.
    /// GeometryChangedEvent is sent in the layout phase, so measuring from it lands in that frame's render. The event does not propagate, so it is registered per element.
    /// </remarks>
    public sealed class TutorialAnchorGeometryWatch
    {
        private readonly VisualElement _coordinateSpace;
        private readonly HashSet<VisualElement> _watched = new();
        private readonly HashSet<VisualElement> _collecting = new();
        private readonly List<VisualElement> _released = new();
        private readonly Subject<Unit> _onGeometryChanged = new();
        private readonly EventCallback<GeometryChangedEvent> _onElementGeometryChanged;

        public IObservable<Unit> OnGeometryChanged => _onGeometryChanged;

        public TutorialAnchorGeometryWatch(VisualElement coordinateSpace)
        {
            _coordinateSpace = coordinateSpace;
            _onElementGeometryChanged = _ => _onGeometryChanged.OnNext(Unit.Default);
        }

        public void BeginCollect()
        {
            _collecting.Clear();
        }

        public void Include(VisualElement anchor)
        {
            // アンカー自身と座標系までの祖先を見張る（祖先が動くとアンカーの layout は変わらず worldBound だけ変わる）
            // Watch the anchor and its ancestors up to the coordinate space (an ancestor move leaves the anchor's layout unchanged)
            for (var node = anchor; node != null && node != _coordinateSpace; node = node.hierarchy.parent)
            {
                _collecting.Add(node);
            }
        }

        public void EndCollect()
        {
            // 今回集めなかった要素から外し、新しく集めた要素へ張る
            // Unregister elements no longer collected, then register newly collected ones
            _released.Clear();
            foreach (var element in _watched)
            {
                if (!_collecting.Contains(element)) _released.Add(element);
            }
            foreach (var element in _released)
            {
                element.UnregisterCallback(_onElementGeometryChanged);
                _watched.Remove(element);
            }
            foreach (var element in _collecting)
            {
                if (_watched.Add(element)) element.RegisterCallback(_onElementGeometryChanged);
            }
        }

        public void Clear()
        {
            BeginCollect();
            EndCollect();
        }
    }
}
