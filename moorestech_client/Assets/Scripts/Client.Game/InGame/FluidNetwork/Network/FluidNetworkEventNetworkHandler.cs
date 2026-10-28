using System;
using Client.Game.Common;
using Client.Game.InGame.Train.Network;
using Client.Game.InGame.Train.Network.Diagnostics;
using Client.Game.InGame.Train.Unit;
using Client.Network.API;
using Cysharp.Threading.Tasks;
using Game.Fluid.Sync.Message;
using MessagePack;
using Server.Event.EventReceive.FluidNetworkSync;
using VContainer.Unity;
using Debug = UnityEngine.Debug;

namespace Client.Game.InGame.FluidNetwork.Network
{
    // 流体網の3イベントを受ける。途中参加の全量は即時に複製を組み、毎tickの差分と再構築の全量は列車の順序待ちキューへ載せて連番どおりに流す
    // Receives the three fluid network events: the join-time full state assembles the replica at once, the per-tick diff and the rebuilt full state ride the train's ordered buffer and flush in sequence order
    // 連番の水位と列車tickの初期化は列車の全量ハンドラが持つ。流体の全量は同じ登録pushで同じ水位を載せて届くので、ここでは初回同期の完了だけを告げる
    // The train's full snapshot handler owns the watermark and tick initialization; the fluid full state arrives in the same registration push with the same watermark, so this only reports initial sync completion
    public sealed class FluidNetworkEventNetworkHandler : IInitializable, IDisposable, IInitialEventApplyWaitTarget
    {
        private readonly IVanillaApiEvent _vanillaApiEvent;
        private readonly FluidNetworkClientCache _cache;
        private readonly TrainUnitFutureMessageBuffer _futureMessageBuffer;
        private readonly TrainUnitTickState _tickState;
        private readonly TrainSynchronizationDiagnostics _diagnostics;
        private IDisposable _fullSnapshotSubscription;
        private IDisposable _tickDiffSubscription;
        private IDisposable _rebuiltFullStateSubscription;

        // 適用完了の通知口。列車・ベルトと同じく、途中参加の全量の適用で満了・失敗で失格になる
        // Completion source for the apply; as with the train and belt, fulfilled by the join-time full state apply and failed on error
        private readonly UniTaskCompletionSource _initialApplyCompletion = new();

        public FluidNetworkEventNetworkHandler(
            IVanillaApiEvent vanillaApiEvent,
            FluidNetworkClientCache cache,
            TrainUnitFutureMessageBuffer futureMessageBuffer,
            TrainUnitTickState tickState,
            TrainSynchronizationDiagnostics diagnostics)
        {
            _vanillaApiEvent = vanillaApiEvent;
            _cache = cache;
            _futureMessageBuffer = futureMessageBuffer;
            _tickState = tickState;
            _diagnostics = diagnostics;
        }

        public void Initialize()
        {
            _fullSnapshotSubscription = _vanillaApiEvent.SubscribeEventResponse(FluidNetworkFullSnapshotEventPacket.EventTag, HandleFullSnapshot);
            _tickDiffSubscription = _vanillaApiEvent.SubscribeEventResponse(FluidNetworkTickEventPacket.TickDiffEventTag, HandleTickDiff);
            _rebuiltFullStateSubscription = _vanillaApiEvent.SubscribeEventResponse(FluidNetworkTickEventPacket.RebuiltFullStateEventTag, HandleRebuiltFullState);
        }

        public UniTask WaitForInitialApplyAsync()
        {
            return _initialApplyCompletion.Task;
        }

        // ネットワーク受信payloadのデシリアライズ(外部境界)と、コピーの組み立て(ApplyFullState)を別々のtryで隔離し、失敗の文言と停止理由で原因を区別する
        // Deserialization of the received payload (external boundary) and assembling the copy (ApplyFullState) are isolated in separate try blocks so the message and stop reason tell the causes apart
        private void HandleFullSnapshot(byte[] payload)
        {
            FluidNetworkFullStateMessagePack message;
            try
            {
                message = MessagePackSerializer.Deserialize<FluidNetworkFullStateMessagePack>(payload);
            }
            catch (Exception deserializeException)
            {
                // 初期全量はInitializeDispatchの同期replayを通るため、再送出すると起動ごと中断し残りのbufferedイベントが永久に配信されない。畳まないと完了ソースがPendingで残り、初期化の待機が無期限になる
                // The initial full state arrives through InitializeDispatch's synchronous replay, so rethrowing would abort startup and strand every remaining buffered event; without folding, the source stays Pending and the init wait never ends
                _initialApplyCompletion.TrySetException(deserializeException);
                Debug.LogError($"[FluidNetworkSync] 途中参加の全量のデシリアライズに失敗しました: {deserializeException}");
                // コピーが無いまま順序適用とハッシュ照合が進むとNREが続くので、列車ごと同期を止める
                // Without a copy, ordered applies and hash checks would keep throwing NREs, so stop the sync together with the train
                _futureMessageBuffer.StopRetainingFutureMessages("FluidNetworkInitialSnapshotFailed");
                return;
            }

            _diagnostics.RecordReceived("FluidNetworkFullSnapshot", message.ServerTick, message.TickSequenceId);
            _tickState.SetMaxBufferedTickUnifiedId(TrainTickUnifiedIdUtility.CreateTickUnifiedId(message.ServerTick, message.TickSequenceId));
            if (!CanApplyInitialSnapshot()) return;

            // 組み立ての失敗も完了ソースへ畳む。購読は EventResponseDispatcher.InvokeIsolated が例外を握るので、畳まないと起動待ちが永遠に終わらない
            // Fold assembly failures into the completion source too: the dispatcher's InvokeIsolated swallows handler exceptions, so without folding the startup wait never ends
            try
            {
                _cache.ApplyFullState(message.ToFullState());
            }
            catch (Exception applyException)
            {
                _initialApplyCompletion.TrySetException(applyException);
                Debug.LogError($"[FluidNetworkSync] 途中参加の全量からコピーを組み立てられませんでした（受信データの破損ではなく組み立て側の不具合）: {applyException}");
                _futureMessageBuffer.StopRetainingFutureMessages("FluidNetworkInitialApplyFailed");
                return;
            }
            _initialApplyCompletion.TrySetResult();

            #region Internal

            bool CanApplyInitialSnapshot()
            {
                if (_initialApplyCompletion.Task.Status == UniTaskStatus.Pending) return true;
                // 失敗・恒久停止の理由は通知済み。完了済みの全量の再適用だけを説明する
                // Failure and permanent-stop reasons were already logged; explain only completed full-state rejection
                if (_initialApplyCompletion.Task.Status == UniTaskStatus.Succeeded && !_tickState.IsPermanentlyWaiting)
                    Debug.LogWarning("[FluidNetworkSync] Ignored join-time full state after initial synchronization completed.");
                return false;
            }

            #endregion
        }

        private void HandleTickDiff(byte[] payload)
        {
            // 外部境界: 受信payloadのデシリアライズだけを隔離する。壊れた差分は黙って落とさず、列車ごと同期を止めて理由を残す
            // External boundary isolating only the deserialization of the received payload; a corrupt diff is not dropped silently but stops the sync together with the train, leaving the reason
            FluidNetworkTickDiffMessagePack message;
            try
            {
                message = MessagePackSerializer.Deserialize<FluidNetworkTickDiffMessagePack>(payload);
            }
            catch (Exception deserializeException)
            {
                Debug.LogError($"[FluidNetworkSync] 流体差分のデシリアライズに失敗しました: {deserializeException}");
                _futureMessageBuffer.StopRetainingFutureMessages("FluidNetworkTickDiffDeserializeFailed");
                return;
            }
            _futureMessageBuffer.EnqueueEvent(message.ServerTick, message.TickSequenceId, new FluidNetworkTickDiffBufferedEvent(_cache, _futureMessageBuffer, _diagnostics, TrainTickUnifiedIdUtility.CreateTickUnifiedId(message.ServerTick, message.TickSequenceId), message.ToDiff()));
        }

        private void HandleRebuiltFullState(byte[] payload)
        {
            // 外部境界: 受信payloadのデシリアライズだけを隔離する(HandleTickDiff と同じ扱い)
            // External boundary isolating only the deserialization of the received payload (handled like HandleTickDiff)
            FluidNetworkFullStateMessagePack message;
            try
            {
                message = MessagePackSerializer.Deserialize<FluidNetworkFullStateMessagePack>(payload);
            }
            catch (Exception deserializeException)
            {
                Debug.LogError($"[FluidNetworkSync] 作り直し後の全量のデシリアライズに失敗しました: {deserializeException}");
                _futureMessageBuffer.StopRetainingFutureMessages("FluidNetworkRebuiltFullStateDeserializeFailed");
                return;
            }
            _futureMessageBuffer.EnqueueEvent(message.ServerTick, message.TickSequenceId, new FluidNetworkRebuiltFullStateBufferedEvent(_cache, message.ToFullState()));
        }

        public void Dispose()
        {
            _fullSnapshotSubscription?.Dispose();
            _tickDiffSubscription?.Dispose();
            _rebuiltFullStateSubscription?.Dispose();
        }
    }
}
