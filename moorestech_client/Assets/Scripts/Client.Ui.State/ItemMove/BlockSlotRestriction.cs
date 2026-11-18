using System.Collections.Generic;

namespace Client.Ui.State.ItemMove
{
    /// <summary>
    /// ブロック側スロットの受け入れ制約。候補と取得状態は具体側（L0-s4b の解決）が確定して渡す
    /// Block-slot acceptance constraint; the concrete resolver (L0-s4b) settles candidates and fetch state
    /// </summary>
    public abstract class BlockSlotRestriction
    {
        public static readonly BlockSlotRestriction Unrestricted = new UnrestrictedBlockSlots();
        public static readonly BlockSlotRestriction InventoryOpenFailed = new BlockInventoryOpenFailed();
        public static readonly BlockSlotRestriction DetailLoading = new BlockDetailLoading();

        private protected BlockSlotRestriction()
        {
        }

        public static BlockSlotRestriction Bound(IReadOnlyList<int> candidateIndices) => new BoundBlockSlots(candidateIndices);
    }

    // 制約なし。全スロットが宛先候補
    // No constraint; every slot is a candidate
    public sealed class UnrestrictedBlockSlots : BlockSlotRestriction
    {
        internal UnrestrictedBlockSlots()
        {
        }
    }

    // 束縛あり。候補の番号だけが宛先（0 件なら受け付ける枠が無い）
    // Bound; only the candidate indices are destinations (zero means no accepting slot)
    public sealed class BoundBlockSlots : BlockSlotRestriction
    {
        public IReadOnlyList<int> CandidateIndices { get; }

        internal BoundBlockSlots(IReadOnlyList<int> candidateIndices)
        {
            CandidateIndices = candidateIndices;
        }
    }

    // ブロックのインベントリを開けなかった
    // The block inventory failed to open
    public sealed class BlockInventoryOpenFailed : BlockSlotRestriction
    {
        internal BlockInventoryOpenFailed()
        {
        }
    }

    // ブロックの詳細を取得中
    // The block detail is still loading
    public sealed class BlockDetailLoading : BlockSlotRestriction
    {
        internal BlockDetailLoading()
        {
        }
    }
}
