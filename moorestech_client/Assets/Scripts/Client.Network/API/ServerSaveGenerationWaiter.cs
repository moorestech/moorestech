using Cysharp.Threading.Tasks;
using MessagePack;
using Server.Event.EventReceive;
using UnityEngine;

namespace Client.Network.API
{
    /// <summary>
    /// セーブを要求し、その要求番号が書き出し完了通知に追いつくまで期限付きで待つ
    /// Requests a save and waits, with a deadline, until its generation is reported written
    /// </summary>
    public sealed class ServerSaveGenerationWaiter
    {
        public enum SaveWaitResult
        {
            Written,
            NoResponse,
            TimedOut,
        }

        private readonly VanillaApi _vanillaApi;
        private long _completedSaveGeneration;

        // 完了通知はSave応答より先に届きうるため、要求前から購読しておく（ゲーム寿命のため購読解除はしない）
        // The completion notice can arrive before the save response, so subscribe before any request (game-lifetime, never unsubscribed)
        public ServerSaveGenerationWaiter(VanillaApi vanillaApi)
        {
            _vanillaApi = vanillaApi;
            _vanillaApi.Event.SubscribeEventResponse(WorldSaveCompletedEventPacket.EventTag, OnWorldSaveCompleted);
        }

        // 上限は実時間の秒。終了処理中のフレーム落ちでも待ち時間が伸び縮みしないよう単位を揃える
        // The budget is real-time seconds, a single unit so frame drops during shutdown never stretch or shrink the wait
        public async UniTask<SaveWaitResult> SaveAndWaitWrittenAsync(float timeoutSeconds)
        {
            // 応答待ちの期限は通信層が持ち、期限切れは null で返る
            // The response wait is bounded by the network layer, which returns null on timeout
            var saveResponse = await _vanillaApi.Response.Save(default);
            if (saveResponse == null) return SaveWaitResult.NoResponse;

            var deadline = Time.realtimeSinceStartup + timeoutSeconds;
            while (_completedSaveGeneration < saveResponse.RequestedSaveGeneration && Time.realtimeSinceStartup < deadline)
            {
                await UniTask.Yield(PlayerLoopTiming.Update);
            }
            return _completedSaveGeneration < saveResponse.RequestedSaveGeneration ? SaveWaitResult.TimedOut : SaveWaitResult.Written;
        }

        private void OnWorldSaveCompleted(byte[] payload)
        {
            var completed = MessagePackSerializer.Deserialize<WorldSaveCompletedEventPacket.WorldSaveCompletedMessagePack>(payload);
            if (_completedSaveGeneration < completed.CompletedSaveGeneration) _completedSaveGeneration = completed.CompletedSaveGeneration;
        }
    }
}
