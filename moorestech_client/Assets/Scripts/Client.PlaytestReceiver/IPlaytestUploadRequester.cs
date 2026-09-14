namespace Client.PlaytestReceiver
{
    // 押し場（起動直後・報告送信直後）が見る面。押す側は走行の組み立ても単線化も知らない
    // The face the two push sites (post-launch, post-report) see; neither knows how a run is built or kept single
    public interface IPlaytestUploadRequester
    {
        void RequestUpload(PlaytestSession session);
    }
}
