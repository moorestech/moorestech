using System;

namespace Game.Block.Blocks.BeltConveyor.Sync.Diff
{
    // 1tick分の機械との搬送差分。搬入はblock更新中に起きた順、搬出は搬送tick中に成立した順
    // One tick's machine handoff diff; inserts in the order they happened during block updates, extracts in the order they settled during the transport tick
    public sealed class BeltTickDiff
    {
        public static readonly BeltTickDiff Empty = new(Array.Empty<BeltMachineInsertRecord>(), Array.Empty<BeltMachineExtractRecord>());

        public readonly BeltMachineInsertRecord[] Inserts;
        public readonly BeltMachineExtractRecord[] Extracts;

        public bool IsEmpty => Inserts.Length == 0 && Extracts.Length == 0;

        public BeltTickDiff(BeltMachineInsertRecord[] inserts, BeltMachineExtractRecord[] extracts)
        {
            Inserts = inserts;
            Extracts = extracts;
        }
    }
}
