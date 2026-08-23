using Client.Game.Common;
using Cysharp.Threading.Tasks;
using Server.Boot;

namespace Client.Starter.Initialization
{
    // 内蔵サーバーの自壊を終了パイプラインへ載せる。自壊はセーブの書き出しを含む
    // Puts the embedded server's fold onto the shutdown pipeline; the fold includes the save flush
    public class EmbeddedServerShutdownParticipant : IGameShutdownParticipant
    {
        private readonly ServerStarter _serverStarter;

        public EmbeddedServerShutdownParticipant(ServerStarter serverStarter)
        {
            _serverStarter = serverStarter;
        }

        public async UniTask<ShutdownFlushResult> FlushOnShutdownAsync()
        {
            var serverFlushResult = await _serverStarter.ShutdownAsync();
            return serverFlushResult == ServerSaveFlushResult.Flushed ? ShutdownFlushResult.Flushed : ShutdownFlushResult.FlushTimedOut;
        }
    }
}
