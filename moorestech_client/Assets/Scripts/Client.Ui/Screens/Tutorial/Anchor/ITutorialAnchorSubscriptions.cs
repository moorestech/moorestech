using System;
using System.Collections.Generic;

namespace Client.Ui.Screens.Tutorial.Anchor
{
    /// <summary>
    /// アンカー追跡の変えの口。Presenter だけが持ち、購読する ID を差し替え、画面が外れたら止める
    /// The change port of anchor tracking; held only by the Presenter to swap subscribed IDs and stop on detach
    /// </summary>
    public interface ITutorialAnchorSubscriptions
    {
        IObservable<TutorialAnchorChange> OnAnchorChanged { get; }
        void SetSubscribedAnchors(IReadOnlyCollection<string> anchorIds);
        void StopTracking();
    }
}
