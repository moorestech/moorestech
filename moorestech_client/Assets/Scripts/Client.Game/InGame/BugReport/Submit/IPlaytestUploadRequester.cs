namespace Client.Game.InGame.BugReport.Submit
{
    // 報告送信側が持つ送信要求の契約。起動直後・報告送信直後が押し、送るかどうか・組立・単線化は実装側が決める
    // The upload-request port owned by the report use case; the push sites never know whether to ship, how a run is built or kept single
    public interface IPlaytestUploadRequester
    {
        void RequestUpload();
    }
}
