using System.Collections.Generic;
using System.Linq;
using Client.Ui.Foundation.Layers;
using Client.Ui.Foundation.Root;
using Client.Ui.Foundation.Stage;
using Client.Ui.Screens.Tutorial.Anchor;
using Client.Ui.State.Tutorial.Overlay;
using UniRx;
using UnityEngine.UIElements;
using VContainer.Unity;

namespace Client.Ui.Screens.Tutorial.Overlay
{
    /// <summary>
    /// 枠の画面を載せ、Model とアンカーの変化を購読して View へ渡す。ack は送らない（D-10）
    /// Mounts the overlay and forwards model and anchor changes to the View; sends no ack (D-10)
    /// </summary>
    public sealed class TutorialOverlayPresenter : IInitializable
    {
        private readonly GameUiRoot _gameUiRoot;
        private readonly TutorialOverlayModel _model;

        public TutorialOverlayPresenter(GameUiRoot gameUiRoot, TutorialOverlayModel model)
        {
            _gameUiRoot = gameUiRoot;
            _model = model;
        }

        public void Initialize()
        {
            // 枠は Portal の最前面（--z-portal-tutorial）。アンカーは stage・viewport の両族から探し、座標はこの層で測る
            // Outlines live in the portal's front layer (--z-portal-tutorial); search anchors across both families and measure in this layer
            var layer = _gameUiRoot.GetLayer(UiLayer.Tutorial, UiFamily.Viewport);
            Mount(layer, _gameUiRoot.GetRootElement(), _model.Current);
        }

        public static TutorialOverlayView Mount(VisualElement layer, VisualElement searchRoot, IReadOnlyReactiveProperty<TutorialOverlayViewModel> model)
        {
            var tracker = new TutorialAnchorTracker(searchRoot, layer);
            var view = new TutorialOverlayView(tracker);
            layer.Add(view);
            tracker.OnAnchorStateChanged.Subscribe(view.RedrawAnchor);
            model.Subscribe(OnModelChanged);
            // 外れている間は毎フレームの測定を止め、載せ直したら今の ViewModel で購読し直す（D-32）
            // Stop per-frame measuring while detached; resubscribe with the current view model on re-attach (D-32)
            view.RegisterCallback<DetachFromPanelEvent>(_ => tracker.StopTracking());
            view.RegisterCallback<AttachToPanelEvent>(_ => OnModelChanged(model.Value));
            return view;

            #region Internal

            void OnModelChanged(TutorialOverlayViewModel current)
            {
                // 外れている間は tracker も Render も触らない（再 attach の AttachToPanelEvent が今の値で入り直す）
                // While detached, touch neither tracker nor Render (re-attach re-enters with the current value)
                if (view.panel == null) return;
                // 購読を差し替えてから描く（差し替えの同期の測定で出た変化は直後の Render が描き直す）
                // Swap subscriptions, then draw (the following Render redraws changes from the synchronous measurement)
                tracker.SetSubscribedAnchors(AnchorIdsOf(current));
                view.Render(current);
            }

            IReadOnlyCollection<string> AnchorIdsOf(TutorialOverlayViewModel current)
            {
                var anchorIds = new HashSet<string>();
                foreach (var outline in current.Outlines) anchorIds.Add(outline.AnchorId);
                foreach (var guide in current.DragGuides)
                {
                    anchorIds.Add(guide.FromAnchorId);
                    anchorIds.Add(guide.ToAnchorId);
                }
                return anchorIds.ToList();
            }

            #endregion
        }
    }
}
