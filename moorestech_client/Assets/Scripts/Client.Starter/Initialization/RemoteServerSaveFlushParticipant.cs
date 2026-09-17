using Client.Game.Common;
using Client.Network.API;
using Cysharp.Threading.Tasks;

namespace Client.Starter.Initialization
{
    // リモート接続時の終了待ち。Save要求の番号が書き出し完了通知に追いつくまで待つ
    // The shutdown wait for a remote connection: waits until the save request's generation is reported written
    public class RemoteServerSaveFlushParticipant : IGameShutdownParticipant
    {
        // 書き出し完了通知を待つ上限秒数。届かなくても終了不能にしない
        // Seconds budget for the completion notice; shutdown must never become impossible
        private const float SaveFlushWaitSeconds = 10f;

        private readonly ServerSaveGenerationWaiter _saveGenerationWaiter;

        public RemoteServerSaveFlushParticipant(VanillaApi vanillaApi)
        {
            _saveGenerationWaiter = new ServerSaveGenerationWaiter(vanillaApi);
        }

        public async UniTask<ShutdownFlushResult> FlushOnShutdownAsync()
        {
            // 応答が無い＝要求の到達すら確認できないため、待たずに上限到達として返す
            // No response means even the request's arrival is unconfirmed, so report the budget as exhausted
            var waitResult = await _saveGenerationWaiter.SaveAndWaitWrittenAsync(SaveFlushWaitSeconds);
            return waitResult == ServerSaveGenerationWaiter.SaveWaitResult.Written ? ShutdownFlushResult.Flushed : ShutdownFlushResult.FlushTimedOut;
        }
    }
}
