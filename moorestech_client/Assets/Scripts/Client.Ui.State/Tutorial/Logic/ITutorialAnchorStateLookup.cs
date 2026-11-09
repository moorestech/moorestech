namespace Client.Ui.State.Tutorial.Logic
{
    /// <summary>
    /// アンカーの最新の解決結果を読む口。帳簿と追跡器が実装し、View が持つ
    /// The read port for an anchor's latest resolution; implemented by the book and the tracker, held by Views
    /// </summary>
    public interface ITutorialAnchorStateLookup
    {
        bool TryGet(string anchorId, out TutorialAnchorState state);
    }
}
