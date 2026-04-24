namespace Server.Boot.Shutdown
{
    // サーバー終了パイプラインのフェーズ順序
    // Server shutdown pipeline phase order
    public enum ShutdownPhase
    {
        StopAcceptingConnections = 100,
        StopUpdate               = 200,
        DisposeSubsystems        = 300,
    }
}
