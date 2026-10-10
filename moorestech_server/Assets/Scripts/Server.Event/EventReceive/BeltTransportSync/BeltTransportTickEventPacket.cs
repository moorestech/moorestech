using Game.Block.Blocks.BeltConveyor.Sync.Diff;
using Game.Block.Blocks.BeltConveyor.Sync.Message;
using Game.Block.Blocks.BeltConveyor.Sync.State;
using Game.Block.Blocks.BeltConveyor.Transport;
using Game.Context;
using Game.Train.Unit;
using MessagePack;
using UniRx;
using UnityEngine;

namespace Server.Event.EventReceive.BeltTransportSync
{
    // 搬送tick直後に、そのtickの機械との搬送差分を列車の連番を1つ使って全員へ送る。差分が空でも送り、クライアントが複製を1tick進める合図になる
    // Right after the transport tick, broadcasts that tick's machine handoff diff under one train sequence id; sent even when empty, as the client's cue to advance its replica one tick
    // 組を作り直したtickは差分の代わりに搬送後の全量を送る。クライアントは複製を全量から組み直し、そのtickの差分は再生しない
    // A rebuilt tick sends the post-tick full state instead of the diff; the client re-assembles its replica from it and replays no diff for that tick
    // 全量のハッシュも搬送tick直後に計算して保持する。次tick冒頭に列車がそのtickのハッシュ束を作るときに読む(冒頭は再構築が先に走り、その時点で計算すると新しい組のハッシュになる)
    // The full-state hash is also computed and held right after the transport tick; the train reads it when building that tick's hash bundle at the next tick head (a rebuild runs first there, so hashing then would hash the new assembly)
    public sealed class BeltTransportTickEventPacket : IBootInitializable
    {
        public const string TickDiffEventTag = "va:event:beltTransportTickDiff";
        public const string RebuiltFullStateEventTag = "va:event:beltTransportRebuiltFullState";

        private readonly EventProtocolProvider _eventProtocolProvider;
        private readonly BeltTransportDatastore _beltTransportDatastore;
        private readonly BeltTransportTickUpdater _beltTransportTickUpdater;
        private readonly TrainUpdateService _trainUpdateService;
        private bool _hasHeldStateHash;
        private uint _heldStateHashTick;
        private uint _heldStateHash;

        public BeltTransportTickEventPacket(
            EventProtocolProvider eventProtocolProvider,
            BeltTransportDatastore beltTransportDatastore,
            BeltTransportTickUpdater beltTransportTickUpdater,
            TrainUpdateService trainUpdateService)
        {
            _eventProtocolProvider = eventProtocolProvider;
            _beltTransportDatastore = beltTransportDatastore;
            _beltTransportTickUpdater = beltTransportTickUpdater;
            _trainUpdateService = trainUpdateService;
        }

        public void Load()
        {
            _beltTransportTickUpdater.OnTransportTickCompleted.Subscribe(OnTransportTickCompleted);
        }

        // 列車がtickのハッシュ束を作るときに呼ぶ。そのtickの搬送直後に保持した全量ハッシュを返す
        // Called when the train builds a tick's hash bundle; returns the full-state hash held right after that tick's transport
        public uint StateHashOf(uint tick)
        {
            if (_hasHeldStateHash && _heldStateHashTick == tick) return _heldStateHash;

            // 保持が無いのは搬送tickがまだ1度も無い最初の列車tick(0)だけ。それ以外は組と列車のtickがずれた異常
            // Nothing is held only at the first train tick (0), before any transport tick; anything else means the assembly and train ticks drifted apart
            if (tick != 0) Debug.LogError($"[BeltTransport] no held state hash for tick {tick} (held: {(_hasHeldStateHash ? _heldStateHashTick.ToString() : "none")}); hashing the current assembly instead.");
            return BeltTransportStateHash.Compute(BeltTransportFullStateCapture.Capture(_beltTransportDatastore.Assembly));
        }

        private void OnTransportTickCompleted(BeltTransportTickReport report)
        {
            // 連番はここで1つ消費する。割り当てた連番は必ず送る(送らないと欠番になりクライアントが止まる)
            // One sequence id is consumed here, and an issued id is always sent (an unsent one is a gap that stops the client)
            var tick = _trainUpdateService.GetCurrentTick();
            var tickSequenceId = _trainUpdateService.NextTickSequenceId();
            if (report.WasRebuilt) SendRebuiltFullState();
            else SendTickDiff(report.Diff);
            if (TrainUpdateService.IsHashBroadcastTick(tick)) HoldStateHash();

            #region Internal

            void SendRebuiltFullState()
            {
                var message = new BeltTransportFullStateMessagePack(tick, tickSequenceId, Capture());
                _eventProtocolProvider.AddBroadcastEvent(RebuiltFullStateEventTag, MessagePackSerializer.Serialize(message));
            }

            void SendTickDiff(BeltTickDiff diff)
            {
                var message = new BeltTransportTickDiffMessagePack(tick, tickSequenceId, diff);
                _eventProtocolProvider.AddBroadcastEvent(TickDiffEventTag, MessagePackSerializer.Serialize(message));
            }

            void HoldStateHash()
            {
                _hasHeldStateHash = true;
                _heldStateHashTick = tick;
                _heldStateHash = BeltTransportStateHash.Compute(Capture());
            }

            BeltTransportFullState Capture()
            {
                return BeltTransportFullStateCapture.Capture(_beltTransportDatastore.Assembly);
            }

            #endregion
        }
    }
}
