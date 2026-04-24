namespace Client.Common.Shutdown
{
    // 終了パイプラインのフェーズ順序。値はフェーズ間隔を空けて将来追加を吸収する
    // Shutdown pipeline phase order. Gaps leave room for future phases
    public enum ShutdownPhase
    {
        BeforeDisconnect  = 0,    // Save ACK 待ち
        Disconnect        = 100,  // ソケットクローズ
        AfterDisconnect   = 200,  // サーバー不要なサブシステム停止
        DisposeSubsystems = 300,  // プロセス kill / Addressables / VContainer scope Dispose
    }
}
