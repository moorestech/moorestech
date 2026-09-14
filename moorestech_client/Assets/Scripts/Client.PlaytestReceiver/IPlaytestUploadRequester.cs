namespace Client.PlaytestReceiver
{
    // 起動直後・報告送信直後が押す面。組立/単線化は知らない
    // The face the two push sites (post-launch, post-report) see; neither knows how a run is built or kept single
    public interface IPlaytestUploadRequester
    {
        void RequestUpload(PlaytestSession session);
    }
}
