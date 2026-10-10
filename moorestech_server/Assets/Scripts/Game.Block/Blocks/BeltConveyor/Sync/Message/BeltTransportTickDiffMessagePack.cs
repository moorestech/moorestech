using System;
using Game.Block.Blocks.BeltConveyor.Sync.Diff;
using MessagePack;

namespace Game.Block.Blocks.BeltConveyor.Sync.Message
{
    // 1tick分の機械との搬送差分の通信形。差分が空でも毎tick送られ、クライアントが複製を1tick進める合図になる
    // Wire form of one tick's machine handoff diff; sent every tick even when empty, as the client's cue to advance its replica one tick
    [MessagePackObject]
    public class BeltTransportTickDiffMessagePack
    {
        [Key(0)] public uint ServerTick { get; set; }
        [Key(1)] public uint TickSequenceId { get; set; }
        [Key(2)] public BeltMachineInsertMessagePack[] Inserts { get; set; }
        [Key(3)] public BeltMachineExtractMessagePack[] Extracts { get; set; }

        [Obsolete("デシリアライズ用のコンストラクタです。基本的に使用しないでください。")]
        public BeltTransportTickDiffMessagePack() { }

        public BeltTransportTickDiffMessagePack(uint serverTick, uint tickSequenceId, BeltTickDiff diff)
        {
            ServerTick = serverTick;
            TickSequenceId = tickSequenceId;
            Inserts = new BeltMachineInsertMessagePack[diff.Inserts.Length];
            for (var i = 0; i < Inserts.Length; i++) Inserts[i] = new BeltMachineInsertMessagePack(diff.Inserts[i]);
            Extracts = new BeltMachineExtractMessagePack[diff.Extracts.Length];
            for (var i = 0; i < Extracts.Length; i++) Extracts[i] = new BeltMachineExtractMessagePack(diff.Extracts[i]);
        }

        public BeltTickDiff ToDiff()
        {
            var inserts = new BeltMachineInsertRecord[Inserts.Length];
            for (var i = 0; i < inserts.Length; i++) inserts[i] = Inserts[i].ToRecord();
            var extracts = new BeltMachineExtractRecord[Extracts.Length];
            for (var i = 0; i < extracts.Length; i++) extracts[i] = Extracts[i].ToRecord();
            return new BeltTickDiff(inserts, extracts);
        }
    }
}
