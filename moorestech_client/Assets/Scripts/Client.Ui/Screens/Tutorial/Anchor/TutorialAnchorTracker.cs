using System;
using System.Collections.Generic;
using System.Linq;
using Client.Ui.Foundation.Anchor;
using Client.Ui.State.Tutorial.Logic;
using UniRx;
using UnityEngine;
using UnityEngine.UIElements;

namespace Client.Ui.Screens.Tutorial.Anchor
{
    /// <summary>
    /// 購読中のアンカーを毎フレームとレイアウト後に測り、変わった時だけ通知する追跡器
    /// Measures subscribed anchors every frame and after layout, notifying only on change
    /// </summary>
    /// <remarks>
    /// 毎フレームの測定はスケジューラ（レイアウト前）で走り、スクロール等の transform とインラインの表示切替を拾う。
    /// 移動・display: none のようにレイアウトが変わるものは GeometryChangedEvent から同じフレームのうちに測り直す。
    /// The per-frame measure runs on the scheduler (before layout) and catches transforms such as scrolling and inline display toggles.
    /// Layout changes such as moves and display: none are re-measured from GeometryChangedEvent within the same frame.
    /// </remarks>
    public sealed class TutorialAnchorTracker : ITutorialAnchorStateLookup
    {
        private readonly VisualElement _searchRoot;
        private readonly VisualElement _coordinateSpace;
        private readonly TutorialAnchorStateBook _book = new();
        private readonly TutorialAnchorGeometryWatch _geometryWatch;
        private readonly Subject<string> _onAnchorChanged = new();
        private string[] _subscribed = Array.Empty<string>();
        private IVisualElementScheduledItem _poll;

        public IObservable<string> OnAnchorStateChanged => _onAnchorChanged;

        public TutorialAnchorTracker(VisualElement searchRoot, VisualElement coordinateSpace)
        {
            _searchRoot = searchRoot;
            _coordinateSpace = coordinateSpace;
            _geometryWatch = new TutorialAnchorGeometryWatch(coordinateSpace);
            _geometryWatch.OnGeometryChanged.Subscribe(_ => MeasureAll());
        }

        public void SetSubscribedAnchors(IReadOnlyCollection<string> anchorIds)
        {
            // 購読の入れ替えでは続くアンカーの状態を保ち、外れたものだけ捨てる
            // Swapping subscriptions keeps continuing anchors and drops only the removed ones
            _subscribed = new HashSet<string>(anchorIds).ToArray();
            _book.KeepOnly(anchorIds);
            MeasureAll();
            // 購読がある間だけ毎フレーム測る（plan §4.5。木の変化を待たずスクロール・移動にも追従する）
            // Measure every frame only while something is subscribed (plan §4.5; follows scroll and moves too)
            _poll ??= _coordinateSpace.schedule.Execute(MeasureAll).Every(0);
            if (_subscribed.Length == 0) _poll.Pause();
            else _poll.Resume();
        }

        public void StopTracking()
        {
            // 画面が外れたら測定を止めて状態を捨てる（載せ直したら Presenter が購読をやり直す。D-32）
            // Once the screen detaches, stop measuring and drop states (the Presenter resubscribes on re-attach; D-32)
            _subscribed = Array.Empty<string>();
            _book.KeepOnly(Array.Empty<string>());
            _geometryWatch.Clear();
            _poll?.Pause();
        }

        public bool TryGet(string anchorId, out TutorialAnchorState state)
        {
            return _book.TryGet(anchorId, out state);
        }

        private void MeasureAll()
        {
            // 通知の購読者が購読を入れ替えても回し続けないよう、今回の配列を持って回る
            // Hold this pass's array so a handler swapping subscriptions does not continue the stale pass
            var subscribed = _subscribed;
            _geometryWatch.BeginCollect();
            foreach (var anchorId in subscribed)
            {
                if (subscribed != _subscribed) return;
                var resolution = TutorialAnchorRegistry.Resolve(_searchRoot, anchorId);
                if (resolution is TutorialAnchorResolution.Unique unique) _geometryWatch.Include(unique.Element);

                // 変わった時だけ記帳・報告・通知する
                // Record, report and notify only on change
                var state = TutorialAnchorMeasure.Measure(resolution, _coordinateSpace);
                if (!_book.ApplyIfChanged(anchorId, state)) continue;
                ReportTransition(anchorId, resolution, state);
                _onAnchorChanged.OnNext(anchorId);
            }
            // 入れ替えられた回は見張りの集計を確定しない（入れ替え側の回が確定済み）
            // A swapped pass leaves the watch alone (the swapping pass already settled it)
            if (subscribed == _subscribed) _geometryWatch.EndCollect();

            #region Internal

            void ReportTransition(string anchorId, TutorialAnchorResolution resolution, TutorialAnchorState state)
            {
                // 遷移した時だけ開発者へ出す。重複は印の付け間違いなのでエラー、未発見は未移植の画面でも起きるので警告
                // Report only on transitions; duplicates are marking bugs (error), missing can be an unported screen (warning)
                if (state.Reason == TutorialAnchorReason.DuplicateAnchor)
                {
                    // 件数は状態に持たせず（比較は理由で足りる）、測った時の引きから読む
                    // The count is not kept in the state (the reason suffices for comparison); read it from this pass's resolution
                    var count = resolution is TutorialAnchorResolution.Duplicate duplicate ? duplicate.Count : 0;
                    Debug.LogError($"[TutorialAnchorTracker] anchor is duplicated ({count}): {anchorId}");
                }
                if (state.Reason == TutorialAnchorReason.Missing) Debug.LogWarning($"[TutorialAnchorTracker] anchor is missing: {anchorId}");
            }

            #endregion
        }
    }
}
