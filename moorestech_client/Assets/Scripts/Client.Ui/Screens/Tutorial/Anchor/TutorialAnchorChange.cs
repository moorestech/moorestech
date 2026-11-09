using Client.Ui.State.Tutorial.Logic;

namespace Client.Ui.Screens.Tutorial.Anchor
{
    /// <summary>
    /// 1 つのアンカーの状態が変わった通知
    /// Notification that one anchor's state changed
    /// </summary>
    public readonly struct TutorialAnchorChange
    {
        public readonly string AnchorId;
        public readonly TutorialAnchorState State;

        public TutorialAnchorChange(string anchorId, TutorialAnchorState state)
        {
            AnchorId = anchorId;
            State = state;
        }
    }
}
