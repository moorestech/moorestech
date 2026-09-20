namespace Client.Game.InGame.BugReport.LastSession
{
    // セッションが実際に遊んでいた退避元。落ちたセッション自身が開始時に記録するので、次の起動の設定でそれを代用しない（ADR 0065・D-C3）
    // The salvage source a session was actually playing from; the session records it itself at start so the next boot's settings never stand in for it (ADR 0065, D-C3)
    public sealed class SessionSnapshotSource
    {
        // リモート接続にはスナップショットを書く内蔵サーバーが居ない。退避先が無いことと退避の失敗を混ぜないための区別
        // A remote connection has no embedded server writing snapshots; this separates "there is no source" from "the salvage failed"
        public bool IsRemoteConnection { get; }

        public string WorldSnapshotDirectory { get; }

        public SessionSnapshotSource(bool isRemoteConnection, string worldSnapshotDirectory)
        {
            IsRemoteConnection = isRemoteConnection;
            WorldSnapshotDirectory = worldSnapshotDirectory;
        }
    }
}
