using Client.Game.Common;
using Cysharp.Threading.Tasks;
using Server.Boot;
using UnityEngine;

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

            // サーバーの3値をそのまま写す。諦めを上限到達へ潰すと「待ち切れなかった」と「保存していない」が見分けられない
            // Map the server's three values as-is; folding a give-up into a timeout hides "not saved" behind "did not finish waiting"
            switch (serverFlushResult)
            {
                case ServerSaveFlushResult.Flushed: return ShutdownFlushResult.Flushed;
                case ServerSaveFlushResult.FlushTimedOut: return ShutdownFlushResult.FlushTimedOut;
                case ServerSaveFlushResult.SaveAbandoned: return ShutdownFlushResult.SaveAbandoned;
            }

            // 未知の値は保存済みを名乗らせない。増えた種別を無音で成功へ倒すと保存されていない世界が成功として閉じる
            // An unknown value must not claim success, or an unsaved world closes as saved when a new kind appears
            Debug.LogError($"サーバーの書き出し結果が未知の値です result:{serverFlushResult}。保存されていない可能性があるものとして扱います");
            return ShutdownFlushResult.SaveAbandoned;
        }
    }
}
