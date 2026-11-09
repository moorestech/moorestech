namespace Client.Ui.State.Tutorial.Overlay
{
    public sealed class TutorialDragGuideCue
    {
        public readonly string Key;
        public readonly string FromAnchorId;
        public readonly string ToAnchorId;

        public TutorialDragGuideCue(string key, string fromAnchorId, string toAnchorId)
        {
            Key = key;
            FromAnchorId = fromAnchorId;
            ToAnchorId = toAnchorId;
        }
    }
}
