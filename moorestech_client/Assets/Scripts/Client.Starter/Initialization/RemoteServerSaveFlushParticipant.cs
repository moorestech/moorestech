using Client.Game.Common;
using Client.Network.API;
using Cysharp.Threading.Tasks;
using MessagePack;
using Server.Event.EventReceive;

namespace Client.Starter.Initialization
{
    // リモート接続時の終了待ち。Save要求の番号が書き出し完了通知に追いつくまで待つ
    // The shutdown wait for a remote connection: waits until the save request's generation is reported written
    public class RemoteServerSaveFlushParticipant : IGameShutdownParticipant
    {
        // 書き出し完了通知を待つ上限フレーム数。届かなくても終了不能にしない
        // Frame budget for the completion notice; shutdown must never become impossible
        private const int SaveFlushWaitFrameLimit = 600;

        private readonly VanillaApi _vanillaApi;
        private long _completedSaveGeneration;

        public RemoteServerSaveFlushParticipant(VanillaApi vanillaApi)
        {
            _vanillaApi = vanillaApi;
            _vanillaApi.Event.SubscribeEventResponse(WorldSaveCompletedEventPacket.EventTag, OnWorldSaveCompleted);
        }

        public async UniTask<ShutdownFlushResult> FlushOnShutdownAsync()
        {
            // 応答が無い＝要求の到達すら確認できないため、待たずに上限到達として返す
            // No response means even the request's arrival is unconfirmed, so report the budget as exhausted
            var saveResponse = await _vanillaApi.Response.Save(default);
            if (saveResponse == null) return ShutdownFlushResult.FlushTimedOut;

            for (var frame = 0; frame < SaveFlushWaitFrameLimit && _completedSaveGeneration < saveResponse.RequestedSaveGeneration; frame++)
            {
                await UniTask.Yield(PlayerLoopTiming.Update);
            }

            return _completedSaveGeneration < saveResponse.RequestedSaveGeneration ? ShutdownFlushResult.FlushTimedOut : ShutdownFlushResult.Flushed;
        }

        private void OnWorldSaveCompleted(byte[] payload)
        {
            var completed = MessagePackSerializer.Deserialize<WorldSaveCompletedEventPacket.WorldSaveCompletedMessagePack>(payload);
            if (_completedSaveGeneration < completed.CompletedSaveGeneration) _completedSaveGeneration = completed.CompletedSaveGeneration;
        }
    }
}
