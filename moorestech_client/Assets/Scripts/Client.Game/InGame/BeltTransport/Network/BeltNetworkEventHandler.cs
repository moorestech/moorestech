using System;
using Client.Game.InGame.Train.Network;
using Client.Network.API;
using Core.BeltTransport;
using Cysharp.Threading.Tasks;
using MessagePack;
using Server.Event.EventReceive;
using Server.Util.MessagePack.BeltTransport;
using UniRx;
namespace Client.Game.InGame.BeltTransport
{
    public sealed class BeltNetworkEventHandler
    {
        private readonly UniTaskCompletionSource _initialApply = new();
        private readonly Subject<BeltNetworkSnapshot> _changed = new();
        public IObservable<BeltNetworkSnapshot> OnStateChanged => _changed;
        public BeltClientReplica Replica { get; private set; }
        public UniTask WaitForInitialApplyAsync() => _initialApply.Task;
        public BeltNetworkEventHandler(IVanillaApiEvent events, TrainUnitFutureMessageBuffer futureMessages)
        {
            // 全初期イベントの再生前にsnapshotと差分を購読する。
            // Subscribe to snapshots and differences before initial event replay.
            events.SubscribeEventResponse(TrainFullSnapshotEventPacket.BeltFullSnapshotEventTag, InitializeSnapshot);
            events.SubscribeEventResponse(BeltTickCompletedEventPacket.EventTag, Receive);
            #region Internal
            void InitializeSnapshot(byte[] payload)
            {
                // 外部snapshotの適用失敗を初回同期の待機へ伝える。
                // Propagate external snapshot failures to the initial synchronization wait.
                try
                {
                    var snapshot = MessagePackSerializer.Deserialize<BeltSnapshotMessagePack>(payload);
                    Replica = new BeltClientReplica(snapshot.ToCore());
                    Replica.OnStateChanged.Subscribe(value => _changed.OnNext(value));
                    _initialApply.TrySetResult();
                }
                catch (Exception exception)
                {
                    _initialApply.TrySetException(exception);
                    UnityEngine.Debug.LogError($"[BeltFullSnapshot] 初期snapshotの適用に失敗しました: {exception}");
                }
            }
            void Receive(byte[] payload)
            {
                var message = MessagePackSerializer.Deserialize<BeltTickMessagePack>(payload);
                // 共通watermark以前はtrainが除外し、以降は共通seqで再現する。
                // Train excludes events at or below the shared watermark; replay later events by shared sequence.
                futureMessages.EnqueueEvent(message.ServerTick, message.TickSequenceId,
                    TrainTickBufferedEvent.Create(() => Replica.Receive(message.ToCore())));
            }
            #endregion
        }
    }
}
