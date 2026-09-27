using Cysharp.Threading.Tasks;
using MessagePack;
using Server.Event.EventReceive;
using UnityEngine;

namespace Client.Network.API
{
    /// <summary>
    /// セーブを要求し、その要求番号が書き出し完了通知に追いつくまで予算付きで待つ
    /// Requests a save and waits, within a budget, until its generation is reported written
    /// 購読を1本にするためゲーム寿命で1インスタンスだけ作り、終了時の待ちとsmokeで共有する
    /// A single game-lifetime instance keeps one subscription, shared by the shutdown wait and the smoke run
    /// </summary>
    public sealed class ServerSaveGenerationWaiter
    {
        public enum SaveWaitResult
        {
            Written,
            NoResponse,
            TimedOut,
        }

        // フレーム数の予算。終了時の待ちは裁定でフレーム上限(600)に一本化されている
        // A frame-count budget; the shutdown wait is adjudicated to a single frame limit (600)
        public readonly struct FrameBudget
        {
            public readonly int Frames;

            public FrameBudget(int frames)
            {
                Frames = frames;
            }
        }

        // 実時間の秒の予算。無人検証のように壁時計で期限を切りたい待ち用
        // A real-time seconds budget, for waits bounded by the wall clock such as the unattended smoke run
        public readonly struct RealtimeBudget
        {
            public readonly float Seconds;

            public RealtimeBudget(float seconds)
            {
                Seconds = seconds;
            }
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

        public async UniTask<SaveWaitResult> SaveAndWaitWrittenAsync(FrameBudget budget)
        {
            var requestedGeneration = await RequestSaveAsync();
            if (requestedGeneration == null) return SaveWaitResult.NoResponse;

            // フレーム数で数える。低fpsでは実時間が伸びるが、それが裁定済みの上限の意味
            // Counted in frames; at low fps the real time stretches, which is exactly what the adjudicated limit means
            for (var frame = 0; frame < budget.Frames && !IsWritten(requestedGeneration.Value); frame++)
            {
                await UniTask.Yield(PlayerLoopTiming.Update);
            }
            return IsWritten(requestedGeneration.Value) ? SaveWaitResult.Written : SaveWaitResult.TimedOut;
        }

        public async UniTask<SaveWaitResult> SaveAndWaitWrittenAsync(RealtimeBudget budget)
        {
            var requestedGeneration = await RequestSaveAsync();
            if (requestedGeneration == null) return SaveWaitResult.NoResponse;

            // 壁時計で期限を切る。フレームレートに依らず同じ秒数で打ち切る
            // Bounded by the wall clock, cutting off after the same seconds regardless of frame rate
            var deadline = Time.realtimeSinceStartup + budget.Seconds;
            while (!IsWritten(requestedGeneration.Value) && Time.realtimeSinceStartup < deadline)
            {
                await UniTask.Yield(PlayerLoopTiming.Update);
            }
            return IsWritten(requestedGeneration.Value) ? SaveWaitResult.Written : SaveWaitResult.TimedOut;
        }

        // 応答待ちの期限は通信層が持ち、期限切れは null で返る
        // The response wait is bounded by the network layer, which returns null on timeout
        private async UniTask<long?> RequestSaveAsync()
        {
            var saveResponse = await _vanillaApi.Response.World.Save(default);
            if (saveResponse == null)
            {
                Debug.LogWarning("[ServerSaveGenerationWaiter] the save request got no response within the packet timeout");
                return null;
            }
            return saveResponse.RequestedSaveGeneration;
        }

        private bool IsWritten(long requestedGeneration)
        {
            return requestedGeneration <= _completedSaveGeneration;
        }

        private void OnWorldSaveCompleted(byte[] payload)
        {
            var completed = MessagePackSerializer.Deserialize<WorldSaveCompletedEventPacket.WorldSaveCompletedMessagePack>(payload);
            if (_completedSaveGeneration < completed.CompletedSaveGeneration) _completedSaveGeneration = completed.CompletedSaveGeneration;
        }
    }
}
