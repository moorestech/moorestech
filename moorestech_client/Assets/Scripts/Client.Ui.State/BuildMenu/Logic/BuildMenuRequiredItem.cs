using System;

namespace Client.Ui.State.BuildMenu.Logic
{
    /// <summary>
    /// エントリの必要素材 1 行。所持数と不足は作る側が決め切り、ロジックは再計算しない
    /// One required-material row; the producer settles held and shortage, and the logic never recomputes them
    /// </summary>
    public sealed class BuildMenuRequiredItem
    {
        public Guid ItemGuid { get; }
        public int Count { get; }
        public int Held { get; }
        public bool Lacking { get; }

        public BuildMenuRequiredItem(Guid itemGuid, int count, int held, bool lacking)
        {
            ItemGuid = itemGuid;
            Count = count;
            Held = held;
            Lacking = lacking;
        }
    }
}
