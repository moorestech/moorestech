using System.Collections.Generic;

namespace Client.Ui.State.Tutorial.Logic
{
    /// <summary>
    /// 購読中のアンカーの最新の解決結果を持ち、同値の再解決を捨てる
    /// Holds the latest resolution of subscribed anchors and drops equal re-resolutions
    /// </summary>
    public sealed class TutorialAnchorStateBook : ITutorialAnchorStateLookup
    {
        private readonly Dictionary<string, TutorialAnchorState> _states = new();

        public bool ApplyIfChanged(string anchorId, TutorialAnchorState state)
        {
            // 同値なら描き直さない（Web の「同値rectの再解決では再描画しない」）
            // Equal values skip redraws (Web: equal rect re-resolution does not redraw)
            if (_states.TryGetValue(anchorId, out var previous) && previous.SameAs(state)) return false;
            _states[anchorId] = state;
            return true;
        }

        public bool TryGet(string anchorId, out TutorialAnchorState state)
        {
            return _states.TryGetValue(anchorId, out state);
        }

        public void KeepOnly(IReadOnlyCollection<string> anchorIds)
        {
            // 購読から外れたものだけを捨て、続くものの状態は保つ
            // Drop only unsubscribed anchors and keep the state of the continuing ones
            var keep = new HashSet<string>(anchorIds);
            var dropped = new List<string>();
            foreach (var anchorId in _states.Keys)
            {
                if (!keep.Contains(anchorId)) dropped.Add(anchorId);
            }
            foreach (var anchorId in dropped) _states.Remove(anchorId);
        }
    }
}
