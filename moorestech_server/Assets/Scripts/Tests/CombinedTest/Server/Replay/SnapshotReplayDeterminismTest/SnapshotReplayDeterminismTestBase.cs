using System;
using System.IO;
using System.Linq;
using Core.Update;
using Game.Paths;
using Game.SaveLoad.Interface;
using Game.SaveLoad.Snapshot;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Server.Boot;
using Server.Boot.Loop.PacketProcessing;
using Server.Boot.Replay;
using MessagePack;
using Server.Protocol;
using Server.Protocol.PacketResponse;
using Tests.Module.TestMod;
using static Tests.CombinedTest.Server.PacketTest.PlaceBlockProtocolTestSupport;

namespace Tests.CombinedTest.Server.Replay
{
    // スナップショットkからパケットを流し直すとk+1と一致する。これが再生の忠実性の唯一の検査
    // Replaying packets from snapshot k must reproduce snapshot k+1; this is the only fidelity check for replay
    public abstract class SnapshotReplayDeterminismTestBase
    {

        // 稼働中の受信経路（ReceiveQueueProcessor）を模し、処理tickで常時記録へ追記してから応答を作る
        // Mimics the live receive path (ReceiveQueueProcessor): append to capture at the processing tick, then produce the response
        protected sealed class RecordedLivePacketEntry : ITickEndPacketEntry
        {
            private readonly PacketResponseCreator _packetResponseCreator;
            private readonly PacketResponseContext _context;
            private readonly byte[] _payload;
            private readonly ReceivedPacketLog _packetLog;

            public bool IsActive => true;

            public RecordedLivePacketEntry(PacketResponseCreator packetResponseCreator, PacketResponseContext context, byte[] payload, ReceivedPacketLog packetLog)
            {
                _packetResponseCreator = packetResponseCreator;
                _context = context;
                _payload = payload;
                _packetLog = packetLog;
            }

            public void Process()
            {
                _packetLog.Append(GameUpdater.CurrentTick, _context.PlayerId ?? 0, _payload);
                _packetResponseCreator.GetPacketResponse(_payload, _context);
            }
        }
    }
}
