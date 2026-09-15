namespace Client.PlaytestReceiver.Http
{
    // 箱の種別。受け口のパス語への変換は PlaytestUploadPath.KindSegment だけが行う
    // The kind of a box; only PlaytestUploadPath.KindSegment turns it into the receiver's path word
    public enum PlaytestUploadKind
    {
        Report,
        Progress,
    }
}
