using Client.Game.Common;
using Client.Network.API;
using Cysharp.Threading.Tasks;

namespace Client.Starter.Initialization
{
    // リモート接続時の終了待ち。Save要求の番号が書き出し完了通知に追いつくまで待つ
    // The shutdown wait for a remote connection: waits until the save request's generation is reported written
    public class RemoteServerSaveFlushParticipant : IGameShutdownParticipant
    {
        // 書き出し完了通知を待つ上限フレーム数（裁定: 2026-08-23 セーブして終了はサーバーのflush完了を待つ）。届かなくても終了不能にしない
        // Frame budget for the completion notice (adjudicated 2026-08-23); shutdown must never become impossible
        private static readonly ServerSaveGenerationWaiter.FrameBudget SaveFlushWaitFrameLimit = new(600);

        private readonly ServerSaveGenerationWaiter _saveGenerationWaiter;

        public RemoteServerSaveFlushParticipant(ServerSaveGenerationWaiter saveGenerationWaiter)
        {
            _saveGenerationWaiter = saveGenerationWaiter;
        }

        public async UniTask<ShutdownFlushResult> FlushOnShutdownAsync()
        {
            // 応答が無い＝要求の到達すら確認できないため、待たずに上限到達として返す
            // No response means even the request's arrival is unconfirmed, so report the budget as exhausted
            var waitResult = await _saveGenerationWaiter.SaveAndWaitWrittenAsync(SaveFlushWaitFrameLimit);
            return waitResult == ServerSaveGenerationWaiter.SaveWaitResult.Written ? ShutdownFlushResult.Flushed : ShutdownFlushResult.FlushTimedOut;
        }
    }
}
