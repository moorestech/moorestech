using System;
using System.Collections.Generic;
using Client.Ui.Foundation.Anchor;
using Client.Ui.State.Tutorial.Logic;
using UniRx;
using UnityEngine;
using UnityEngine.UIElements;

namespace Client.Ui.Screens.Tutorial.Anchor
{
    /// <summary>
    /// 購読中のアンカーを毎フレーム測り、変わった時だけ通知する追跡器
    /// Measures subscribed anchors every frame and notifies only on change
    /// </summary>
    public sealed class TutorialAnchorTracker : ITutorialAnchorStateLookup, ITutorialAnchorSubscriptions
    {
        private readonly VisualElement _searchRoot;
        private readonly VisualElement _coordinateSpace;
        private readonly TutorialAnchorStateBook _book = new();
        private readonly HashSet<string> _subscribed = new();
        private readonly Subject<TutorialAnchorChange> _onAnchorChanged = new();
        private IVisualElementScheduledItem _poll;

        public IObservable<TutorialAnchorChange> OnAnchorChanged => _onAnchorChanged;

        public TutorialAnchorTracker(VisualElement searchRoot, VisualElement coordinateSpace)
        {
            _searchRoot = searchRoot;
            _coordinateSpace = coordinateSpace;
        }

        public void SetSubscribedAnchors(IReadOnlyCollection<string> anchorIds)
        {
            // 購読の入れ替えでは続くアンカーの状態を保ち、外れたものだけ捨てる
            // Swapping subscriptions keeps continuing anchors and drops only the removed ones
            _subscribed.Clear();
            _subscribed.UnionWith(anchorIds);
            _book.KeepOnly(anchorIds);
            MeasureAll();
            // 購読がある間だけ毎フレーム測る（plan §4.5。木の変化を待たずスクロール・移動にも追従する）
            // Measure every frame only while something is subscribed (plan §4.5; follows scroll and moves too)
            _poll ??= _coordinateSpace.schedule.Execute(MeasureAll).Every(0);
            if (_subscribed.Count == 0) _poll.Pause();
            else _poll.Resume();
        }

        public void StopTracking()
        {
            // 画面が外れたら測定を止めて状態を捨てる（載せ直したら Presenter が購読をやり直す。D-32）
            // Once the screen detaches, stop measuring and drop states (the Presenter resubscribes on re-attach; D-32)
            _subscribed.Clear();
            _book.KeepOnly(Array.Empty<string>());
            _poll?.Pause();
        }

        public bool TryGet(string anchorId, out TutorialAnchorState state)
        {
            return _book.TryGet(anchorId, out state);
        }

        private void MeasureAll()
        {
            foreach (var anchorId in _subscribed)
            {
                var state = TutorialAnchorMeasure.Measure(_searchRoot, _coordinateSpace, anchorId);
                if (!_book.ApplyIfChanged(anchorId, state)) continue;
                ReportTransition(anchorId, state);
                _onAnchorChanged.OnNext(new TutorialAnchorChange(anchorId, state));
            }
        }

        private void ReportTransition(string anchorId, TutorialAnchorState state)
        {
            // 遷移した時だけ開発者へ出す。重複は印の付け間違いなのでエラー、未発見は未移植の画面でも起きるので警告
            // Report only on transitions; duplicates are marking bugs (error), missing can be an unported screen (warning)
            if (state.Reason == TutorialAnchorReason.DuplicateAnchor)
            {
                // 件数は状態に持たせず（比較は理由で足りる）、報告の時だけ引き直す
                // The count is not kept in the state (the reason suffices for comparison); re-resolve only to report
                var count = TutorialAnchorRegistry.Resolve(_searchRoot, anchorId) is TutorialAnchorResolution.Duplicate duplicate ? duplicate.Count : 0;
                Debug.LogError($"[TutorialAnchorTracker] anchor is duplicated ({count}): {anchorId}");
            }
            if (state.Reason == TutorialAnchorReason.Missing) Debug.LogWarning($"[TutorialAnchorTracker] anchor is missing: {anchorId}");
        }
    }

}
